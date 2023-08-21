using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Runtime.CredentialManagement;
using Amazon.S3;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using AutoMapper;
using HotelMan_HotelAdmin.Models;
using HttpMultipartParser;
using Newtonsoft.Json.Linq;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace HotelMan_HotelAdmin
{
    public class HotelAdmin
    {
        public async Task<APIGatewayProxyResponse> ListHotels(APIGatewayProxyRequest request, ILambdaContext context)
        {
            var response = new APIGatewayProxyResponse()
            {
                Headers = new Dictionary<string, string>(),
                StatusCode = 200,
            };
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Headers", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "OPTIONS, get");
            response.Headers.Add("Content-Type", "application/json");
            try
            { 
                var token = request.QueryStringParameters["token"];
                var tokenDetails = new JwtSecurityToken(token);
                var demystified = tokenDetails.Claims.Where(c => c.Type == "sub");

                var userId = demystified.FirstOrDefault()?.Value;
                var dbRegion = Environment.GetEnvironmentVariable("TABLE_REGION");
                var dbClient = new AmazonDynamoDBClient(RegionEndpoint.GetBySystemName(dbRegion));

                var dbContext = new DynamoDBContext(dbClient);
                Console.WriteLine("=====" + JsonSerializer.Serialize(userId) + " USER ID");
                var hotels = await dbContext.ScanAsync<Hotel>(new[] { new ScanCondition("userId", ScanOperator.Equal, userId) }).GetRemainingAsync();

                response.Body = JsonSerializer.Serialize(hotels);
            }catch(Exception ex)
            {
                response.Body = JsonSerializer.Serialize(new {Error = ex.Message });
                response.StatusCode = 400;

            }

            return response;
        }

        public async Task<APIGatewayProxyResponse> AddHotel(APIGatewayProxyRequest request, ILambdaContext context)
        {
            var response = new APIGatewayProxyResponse()
            {
                Headers = new Dictionary<string, string>(),
                StatusCode = 200,
            };

            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Headers", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "OPTIONS, post");

            var bodyContent = request.IsBase64Encoded ? Convert.FromBase64String(request.Body) : Encoding.UTF8.GetBytes(request.Body);
            using var memStream = new MemoryStream(bodyContent);
            var formData = MultipartFormDataParser.Parse(memStream);
            var hotelName = formData.GetParameterValue(name: "hotelName");
            var hotelRating = formData.GetParameterValue(name: "hotelRating");
            var hotelCity = formData.GetParameterValue(name: "hotelCity");
            var hotelPrice = formData.GetParameterValue(name: "hotelPrice");

            var file = formData.Files.FirstOrDefault();
            var filename = file.FileName;

            await using var fileContentStream = new MemoryStream();
            await file.Data.CopyToAsync(fileContentStream);
            fileContentStream.Position = 0;

            var userId = formData.GetParameterValue(name: "userId");
            var idToken = formData.GetParameterValue(name: "idToken");

            var token = new JwtSecurityToken(idToken);

            var group = token.Claims.FirstOrDefault(x => x.Type == "cognito:groups");

            if (group == null || group.Value != "Admin")
            {
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                response.Body = JsonSerializer.Serialize(new
                {
                    error = "UnAuthorized. Must be member of Admin group"
                });
            }

            var region = Environment.GetEnvironmentVariable("AWS_REGIONS");
            var dbRegion = Environment.GetEnvironmentVariable("TABLE_REGION");
            var buketName = Environment.GetEnvironmentVariable("BUCKET_NAME");

            var client = new AmazonS3Client(RegionEndpoint.GetBySystemName(region));
            var dbClient = new AmazonDynamoDBClient(RegionEndpoint.GetBySystemName(dbRegion));


            try
            {
                var putObjectRes = await client.PutObjectAsync(new Amazon.S3.Model.PutObjectRequest()
                {
                    BucketName = buketName,
                    Key = file.FileName,
                    InputStream = fileContentStream,
                    AutoCloseStream = true
                });

                Console.WriteLine("Executed Hotels");

                var hotel = new Hotel
                {
                    Name = hotelName,
                    CityName = hotelCity,
                    id = Guid.NewGuid().ToString(),
                    userId = userId,
                    FileName = file.FileName,
                    Rating = int.Parse(hotelRating),
                    Price = int.Parse(hotelPrice)
                };

                var dbContext = new DynamoDBContext(dbClient);
                Console.WriteLine(JsonSerializer.Serialize(hotel) + " HOlla");
                await dbContext.SaveAsync(hotel);

                var mapperConfig = new AutoMapper.MapperConfiguration(
                    cfg => cfg.CreateMap<Hotel, HotelCreatedEvent>().ForMember(
                        dest => dest.CreationDateTIme, 
                        opt => opt.MapFrom( 
                            src => DateTime.Now
                            )));

                var mapper = new Mapper(mapperConfig);
                var hotelCreatedEvent = mapper.Map<Hotel, HotelCreatedEvent>(hotel);

                var snsClient = new AmazonSimpleNotificationServiceClient();
                var publishResponse = await snsClient.PublishAsync(new PublishRequest
                {
                    TopicArn = Environment.GetEnvironmentVariable("TopicArn"),
                    Message = JsonSerializer.Serialize(hotelCreatedEvent)
                });
                Console.WriteLine("Topic ARn was published " + JsonSerializer.Serialize(publishResponse));

            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                throw;
            }

            Console.WriteLine("I went okay");
            return response;
        }
    }
}