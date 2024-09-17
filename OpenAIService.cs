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
using Azure.Data.Tables;
using ScoutTalentBot.Model;
using Microsoft.Rest;

namespace HandlingAttachmentsBot
{
    public class OpenAIService
    {
        //TODO: 2- is not used, and endpoint url used directly, needs to be reviewied
        private readonly Azure.AI.OpenAI.AzureOpenAIClient _openAIClient;
        private readonly string _apiKey;
        private readonly string _endPoint;
        private readonly string _deploymentname;
        private readonly TableServiceClient _tableServiceClient;

        public class OpenAIRequest
        {
            public string Prompt { get; set; }
            public int MaxTokens { get; set; } = 150;
        }

        public OpenAIService(string openAiEndpoint, string openAiKey, string deploymentname, TableServiceClient tableServiceClient)
        {
            _apiKey = openAiKey;
            _endPoint = openAiEndpoint;
            _deploymentname = deploymentname;
            _openAIClient = new AzureOpenAIClient(new Uri(openAiEndpoint), new AzureKeyCredential(openAiKey));
            _tableServiceClient = tableServiceClient;
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

            var tableClient = _tableServiceClient.GetTableClient(tableName: "InterviewQuestions");
            // Create the table if it doesn't exist
            await tableClient.CreateIfNotExistsAsync();
            try
            {
                // Create a new entity with job description and questions
                var questionEntity = new InterviewQuestionEntity
                {
                    PartitionKey = "PD-Questions",  // or use HR's ID to group questions
                    RowKey = Guid.NewGuid().ToString(), // Unique ID for each set of questions
                    JobDescription = jobDescription,
                    Questions = completion.Content[0].Text,
                    CreatedDate = DateTime.UtcNow,
                    Timestamp = DateTime.UtcNow

                };

                // Add the entity to the table
                await tableClient.AddEntityAsync(questionEntity);
            }
            catch (Exception ex)
            {

                var msg = ex.Message.ToString();

            }
            return $"{completion.Role.ToString()}: {completion.Content[0].Text}";

        }



    }
}

