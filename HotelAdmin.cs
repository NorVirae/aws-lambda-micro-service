using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using System.Text;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace HotelMan_HotelAdmin
{
    public class HotelAdmin
    {
        public APIGatewayProxyResponse AddHotel(APIGatewayProxyRequest request, ILambdaContext context)
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



            Console.WriteLine("OK");
            return response;
        }
    }
}