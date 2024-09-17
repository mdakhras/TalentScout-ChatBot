// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using System.Configuration;
using Azure.AI.FormRecognizer;
using System;
using Azure;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using HandlingAttachmentsBot;
using Azure.Data.Tables;

namespace Microsoft.BotBuilderSamples
{
    public class Startup
    {

        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            #region add cloud Client service


            // Blob Service
            // Read the connection string from appsettings.json
            var blobConnectionString = Configuration.GetSection("AzureBlobStorage:ConnectionString").Value;
            //ConfigurationExtensions.GetRequiredSection<string>("AzureBlobStorage:ConnectionString");

            // Create BlobServiceClient using the connection string from appsettings.json
            var blobServiceClient = new BlobServiceClient(blobConnectionString);

            // Add BlobServiceClient as a singleton service
            services.AddSingleton(blobServiceClient);


            //FormRecognizerService
            // Read the EndPoint from appsettings.json
            var FormRecognizerEndPoint = Configuration.GetSection("FormRecognizer:Endpoint").Value;

            // Read the ApiKey from appsettings.json
            var FormRecognizerApiKey = Configuration.GetSection("FormRecognizer:ApiKey").Value;

            //TODO:3- check how to use already Defined Client, instaead of creating custom service
            //// Create FormRecognizerServiceClient using the connection string from appsettings.json
            //var FormRecognizerClient = new FormRecognizerClient(new Uri(FormRecognizerEndPoint), new AzureKeyCredential(FormRecognizerApiKey));
            //services.AddSingleton(FormRecognizerClient);

            //formRecognizerService
            var formRecognizerService = new FormRecognizerService(FormRecognizerEndPoint, FormRecognizerApiKey);
            services.AddSingleton(formRecognizerService);

            //TableService
            // Read the connection string from appsettings.json
            var tableServiceClient = new TableServiceClient(blobConnectionString);
            services.AddSingleton(tableServiceClient);


            //OPenAI Service
            // Read the EndPoint from appsettings.json
            var openAIServiceEndPoint = Configuration.GetSection("AiService:Endpoint").Value;

            // Read the ApiKey from appsettings.json
            var openAIServiceEndPointApiKey = Configuration.GetSection("AiService:ApiKey").Value;

            // Read the Model DeploymentName from appsettings.json
            var DeploymentName = Configuration.GetSection("AiService:DeploymentName").Value;

            var openAIService = new OpenAIService(openAIServiceEndPoint, openAIServiceEndPointApiKey, DeploymentName, tableServiceClient);
            services.AddSingleton(openAIService);



            #endregion



            services.AddHttpClient().AddControllers().AddNewtonsoftJson(options =>
            {
                options.SerializerSettings.MaxDepth = HttpHelper.BotMessageSerializerSettings.MaxDepth;
            });

            // Create the Bot Framework Authentication to be used with the Bot Adapter.
            services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();

            // Create the Bot Adapter with error handling enabled.
            services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();

            // Create the bot as a transient. In this case the ASP Controller is expecting an IBot.
            services.AddTransient<IBot, AttachmentsBot>();


            
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseDefaultFiles()
                .UseStaticFiles()
                .UseRouting()
                .UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                });

            // app.UseHttpsRedirection();
        }
    }
}
