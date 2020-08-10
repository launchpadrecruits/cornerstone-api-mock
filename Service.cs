using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.ServiceModel;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace csapi
{
    public enum InterviewType
    {
        LiveVideo,
        OnDemand
    }

    public class Reviewer
    {
        public string ReviewerEmail { get; set; }
        public string ReviewerName { get; set; }
    }

    public class CallbackDetails
    {
        public string CallbackUrl { get; set; }
    }

    public class AssignApplicantRequest
    {
        public InterviewType InterviewType { get; set; }
        public string InterviewId { get; set; }
        public string ApplicantFirstName { get; set; }
        public string ApplicantLastName { get; set; }
        public string ApplicantEmail { get; set; }
        public string PrimaryOwnerEmail { get; set; }
        public string RecruiterEmail { get; set; }
        public string JobRequisitionId { get; set; }
        public List<Reviewer> Reviewers { get; set; }
        public DateTime InterviewStartDate { get; set; }
        public DateTime InterviewEndDate { get; set; }
        public CallbackDetails CallbackData { get; set; }
    }

    public class AssignApplicantResponse
    {
        public string InterviewUrl { get; set; }
        public string RecruiterUrl { get; set; }
    }

    [ServiceContract]
    public interface IService
    {
        [OperationContract]
        string GetVersion();

        [OperationContract]
        AssignApplicantResponse AssignApplicant(AssignApplicantRequest req);
    }

    [ApiController]
    [Route("/interview")]
    public class RestHandler : ControllerBase
    {
        private AmazonDynamoDBClient _dynamoDbClient = new AmazonDynamoDBClient();

        [HttpGet]
        public string GetVersion()
        {
            return "0.0.1";
        }

        [HttpGet("{id}")]
        public ContentResult ShowInterview(string id)
        {
            return new ContentResult
            {
                ContentType = "text/html",
                StatusCode = 200,
                Content = @"<html>
    <body>
    <form method='POST'>
        <h1>Rate with:</h1><br/>
        <input type='radio' name='score' value='0'>0</input><br/>
        <input type='radio' name='score' value='100'>100</input><br/>
        <input type='submit' name='review' value='Review' />
    </form>
</body>
</html>",
            };
        }

        [HttpPost("{id}")]
        [Consumes("application/x-www-form-urlencoded")]
        public ContentResult Rate(string id, [FromForm] int score)
        {
            var stage = Environment.GetEnvironmentVariable("STAGE") ?? "dev";

            GetItemRequest itemRequest = new GetItemRequest()
            {
                TableName = $"cs-req-{stage}",
                Key = new Dictionary<string, AttributeValue>()
                {
                    ["id"] = new AttributeValue(id),
                },
            };

            var result = _dynamoDbClient.GetItemAsync(itemRequest);

            result.Wait();

            Console.WriteLine("result: " + result.Result);

            foreach (KeyValuePair<string, AttributeValue> kvp in result.Result.Item)
            {
                Console.WriteLine($"kvp.key: {kvp.Key} value: {kvp.Value.S}");
            }

            string callback_url = result?.Result?.Item["callback_url"]?.S;

            if (null != callback_url)
            {
                var httpClient = new HttpClient();

                httpClient.BaseAddress = new Uri(callback_url);
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
                
                Dictionary<string, object> req =
                    JsonConvert.DeserializeObject<Dictionary<string, object>>(result.Result.Item["req"].S);

                Dictionary<string, object> payload = new Dictionary<string, object>()
                {
                    ["req"] = req,
                    ["score"] = score,
                };

                string payloadAsString = JsonConvert.SerializeObject(payload);

                httpClient.PostAsync("", new StringContent(payloadAsString)).Wait();
            }

            return new ContentResult
            {
                ContentType = "text/html",
                StatusCode = 200,
                Content = $"<html><body><h1>Setting score: {score} for {id}</body></h1></html>",
            };
        }
    }

    public class Service : IService
    {
        private AmazonSimpleNotificationServiceClient _snsClient = new AmazonSimpleNotificationServiceClient();

        private AmazonDynamoDBClient _dynamoDbClient = new AmazonDynamoDBClient();

        public string GetVersion()
        {
            return "0.0.1";
        }

        public AssignApplicantResponse AssignApplicant(AssignApplicantRequest req)
        {
            var stage = Environment.GetEnvironmentVariable("STAGE") ?? "dev";

            var id = System.Guid.NewGuid().ToString();

            var baseUrl =
                (Environment.GetEnvironmentVariable("BASE_URL") ??
                 "https://5s99go0br9.execute-api.us-east-1.amazonaws.com/dev").TrimEnd('/');

            var interviewUrl = $"{baseUrl}/interview/{id}";

            var reqAsString = JsonConvert.SerializeObject(req);

            var itemAttributes = new Dictionary<string, AttributeValue>
            {
                ["id"] = new AttributeValue(id),
                ["req"] = new AttributeValue(reqAsString),
                ["interview_url"] = new AttributeValue(interviewUrl),
            };

            if (!string.IsNullOrWhiteSpace(req?.CallbackData?.CallbackUrl))
            {
                itemAttributes["callback_url"] = new AttributeValue(req.CallbackData.CallbackUrl);
            }

            _dynamoDbClient.PutItemAsync(new PutItemRequest($"cs-req-{stage}", itemAttributes)).Wait();

            var topicArn = Environment.GetEnvironmentVariable("TOPIC_ARN") ??
                           $"arn:aws:sns:us-east-1:235368163414:cs-api-{stage}";

            var message = $"Here's a new interview for meta:\r\n{reqAsString}\r\n\r\nRate at {interviewUrl}";

            var publishRequest = new PublishRequest(topicArn, message)
            {
                Subject = $"New Interview (id: {id})"
            };

            _snsClient.PublishAsync(publishRequest).Wait();

            return new AssignApplicantResponse()
            {
                InterviewUrl = interviewUrl,
                RecruiterUrl = interviewUrl
            };
        }
    }
}