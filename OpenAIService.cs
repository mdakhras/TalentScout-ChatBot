using Azure;
using System.Threading.Tasks;
using System;
using OpenAI.Chat;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json;
using System.Text;
using Azure.AI.OpenAI;

namespace HandlingAttachmentsBot
{
    public class OpenAIService
    {
        //TODO: 2- is not used, and endpoint url used directly, needs to be reviewied
        private readonly Azure.AI.OpenAI.AzureOpenAIClient _openAIClient;
        private readonly string _apiKey;
        private readonly string _endPoint;
        private readonly string _deploymentname;

        public class OpenAIRequest
        {
            public string Prompt { get; set; }
            public int MaxTokens { get; set; } = 150;
        }

        public OpenAIService(string openAiEndpoint, string openAiKey, string deploymentname)
        {

            //_openAIClient = new(new Uri(openAiEndpoint), new AzureKeyCredential(openAiKey));
            _apiKey = openAiKey;
            _endPoint= openAiEndpoint;
            _deploymentname = deploymentname;
            _openAIClient = new AzureOpenAIClient(new Uri(openAiEndpoint), new AzureKeyCredential(openAiKey));
        }

        public async Task<string> GenerateInterviewQuestions(string jobDescription)
        {
                  
            ChatClient chatClient = _openAIClient.GetChatClient(_deploymentname);
            ChatCompletion completion = chatClient.CompleteChat(
              new ChatMessage[] {
                     new SystemChatMessage($"Based on the following job description, generate five interview questions to ask candidates:\n{jobDescription}"),
              },
              new ChatCompletionOptions()
              {
                  //PastMessages = 10,
                  Temperature = (float)0.7,
                  MaxTokens = 800,
                  // StopSequences = [],
                  // NucleusSamplingFactor = (float)0.95,
                  FrequencyPenalty = (float)0,
                  PresencePenalty = (float)0,
              }
            );

            return $"{completion.Role.ToString()}: {completion.Content[0].Text}";

        }



    }
}
