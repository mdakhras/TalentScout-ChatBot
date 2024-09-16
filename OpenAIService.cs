using Azure;
using System.Threading.Tasks;
using System;
using OpenAI.Chat;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json;
using System.Text;

namespace HandlingAttachmentsBot
{
    public class OpenAIService
    {
        //TODO: 2- is not used, and endpoint url used directly, needs to be reviewied
        private readonly Azure.AI.OpenAI.AzureOpenAIClient _openAIClient;
        private readonly string _apiKey;
        private readonly string _endPoint;
        public class OpenAIRequest
        {
            public string Prompt { get; set; }
            public int MaxTokens { get; set; } = 150;
        }

        public OpenAIService(string openAiEndpoint, string openAiKey)
        {

            _openAIClient = new(new Uri(openAiEndpoint), new AzureKeyCredential(openAiKey));
            _apiKey = openAiKey;
            _endPoint= openAiEndpoint;
            //_openAIClient = new AzureOpenAIClient(new Uri(openAiEndpoint), new AzureKeyCredential(openAiKey));
        }

        public async Task<string> GenerateInterviewQuestions(string jobDescription)
        {

            var client = new HttpClient();
            var apiKey = _apiKey;
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var prompt = $"Based on the following job description, generate interview questions to ask candidates:\n{jobDescription}";

            var request = new OpenAIRequest
            {
                Prompt = prompt
            };

            var jsonContent = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
            //https://YOUR_OPENAI_API_ENDPOINT/openai/deployments/YOUR_DEPLOYMENT_ID/completions
            var response = await client.PostAsync(_endPoint, jsonContent);
            var responseContent = await response.Content.ReadAsStringAsync();

            return responseContent; // Parse and extract questions from response

        }



    }
}
