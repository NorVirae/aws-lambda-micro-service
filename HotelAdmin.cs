using Amazon;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Runtime.CredentialManagement;
using Amazon.S3;
using HttpMultipartParser;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace HotelMan_HotelAdmin
{
    public class HotelAdmin
    {
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

            var bodyContent = request.IsBase64Encoded? Convert.FromBase64String(request.Body): Encoding.UTF8.GetBytes(request.Body);
            using var memStream = new MemoryStream(bodyContent);
            var formData = MultipartFormDataParser.Parse(memStream);
            var hotelName = formData.GetParameterValue(name:"hotelName");
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

            if (group == null || group.Value != " Admin")
            {
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                response.Body = JsonSerializer.Serialize( new
                {
                    error = "UnAuthorized. Must be member of Admin group"
                });
            }

            var region = Environment.GetEnvironmentVariable("AWS_REGION");
            var buketName = Environment.GetEnvironmentVariable("BUCKET_NAME");

            var client = new AmazonS3Client(RegionEndpoint.GetBySystemName(region));
            await client.PutObjectAsync(new Amazon.S3.Model.PutObjectRequest()
            {
                BucketName = buketName,
                Key = file.FileName,
                InputStream = fileContentStream,
                AutoCloseStream = true
            });

            Console.WriteLine("OK");
            return response;
        }
    }
}