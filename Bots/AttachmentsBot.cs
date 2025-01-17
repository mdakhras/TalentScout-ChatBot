﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.AI.FormRecognizer;
using Azure.Storage.Blobs;
using HandlingAttachmentsBot;
using HandlingAttachmentsBot.Model;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Schema;


namespace Microsoft.BotBuilderSamples
{
    // Represents a bot that processes incoming activities.
    // For each user interaction, an instance of this class is created and the OnTurnAsync method is called.
    // This is a Transient lifetime service. Transient lifetime services are created
    // each time they're requested. For each Activity received, a new instance of this
    // class is created. Objects that are expensive to construct, or have a lifetime
    // beyond the single turn, should be carefully managed.
 
    public class AttachmentsBot : ActivityHandler
    {
        private static BlobServiceClient _blobServiceClient;
        private static FormRecognizerService _formRecognizerService;
        private static OpenAIService _openAIService;
        private static string _containerName = "pdf-jobs"; // Name of the container in your blob storage
        private readonly UserState _userState;
        private readonly IStatePropertyAccessor<UserProfile> _userProfileAccessor;

        public AttachmentsBot(BlobServiceClient blobServiceClient, FormRecognizerService formRecognizerService,OpenAIService openAIService, UserState userState)
        {
            _blobServiceClient = blobServiceClient;
            _formRecognizerService = formRecognizerService;
            _openAIService = openAIService;
            _userState = userState;
            _userProfileAccessor = _userState.CreateProperty<UserProfile>("UserProfile");
        }
        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            //await SendWelcomeMessageAsync(turnContext, cancellationToken);
            var userProfile = await _userProfileAccessor.GetAsync(turnContext, () => new UserProfile());

            if (string.IsNullOrEmpty(userProfile.UserType))
            {
                // Prompt the user to select their type
                var reply = MessageFactory.Text("Welcome! Please select your user type:");
                reply.SuggestedActions = new SuggestedActions()
                {
                    Actions = new List<CardAction>()
            {
                new CardAction() { Title = "Candidate", Type = ActionTypes.ImBack, Value = "Candidate" },
                new CardAction() { Title = "HR Assitant", Type = ActionTypes.ImBack, Value = "HR Assistant" },
                //new CardAction() { Title = "Administrator", Type = ActionTypes.ImBack, Value = "Administrator" },
            },
                };
                await turnContext.SendActivityAsync(reply, cancellationToken);
            }
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {


            var userProfile = await _userProfileAccessor.GetAsync(turnContext, () => new UserProfile());

            if (string.IsNullOrEmpty(userProfile.UserType))
            {
                // Assume the user's response is their selected type
                userProfile.UserType = turnContext.Activity.Text;
                await _userProfileAccessor.SetAsync(turnContext, userProfile, cancellationToken);
                await _userState.SaveChangesAsync(turnContext, cancellationToken: cancellationToken);

                //show messag based on user type
                if(userProfile.UserType.Equals("HR Assistant"))
                {
                    // Interviewer-specific logic
                    await turnContext.SendActivityAsync("Hello HR Assitant! Would you like to generate interview questions? please upload Post Description here! \"", cancellationToken: cancellationToken);
                   
                }
                else if (userProfile.UserType.Equals("Candidate"))
                {
                    // Candidate-specific logic
                    await turnContext.SendActivityAsync("Hello Candidate! How can I assist you today?", cancellationToken: cancellationToken);
                }
            }
           
                // Continue with behavior based on user type
                await HandleUserTypeSpecificLogic(turnContext, userProfile, cancellationToken);
           

           
        }
  
        private static async Task DisplayOptionsAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            // Create a HeroCard with options for the user to interact with the bot.
            var card = new HeroCard
            {
                Text = "You can upload an image or select one of the following choices",
                Buttons = new List<CardAction>
                {
                    // Note that some channels require different values to be used in order to get buttons to display text.
                    // In this code the emulator is accounted for with the 'title' parameter, but in other channels you may
                    // need to provide a value for other parameters like 'text' or 'displayText'.
                    new CardAction(ActionTypes.ImBack, title: "1. Inline Attachment", value: "1"),
                    new CardAction(ActionTypes.ImBack, title: "2. Internet Attachment", value: "2"),
                    new CardAction(ActionTypes.ImBack, title: "3. Uploaded Attachment", value: "3"),
                },
            };

            var reply = MessageFactory.Attachment(card.ToAttachment());
            await turnContext.SendActivityAsync(reply, cancellationToken);
        }

        // Greet the user and give them instructions on how to interact with the bot.
        private static async Task SendWelcomeMessageAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            foreach (var member in turnContext.Activity.MembersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(
                        $"Welcome to AttachmentsBot {member.Name}." +
                        $" This bot will introduce you to Attachments." +
                        $" Please select an option",
                        cancellationToken: cancellationToken);
                    await DisplayOptionsAsync(turnContext, cancellationToken);
                }
            }
        }

        // Given the input from the message, create the response.
        private static async Task<IMessageActivity> ProcessInput(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var activity = turnContext.Activity;
            IMessageActivity reply = null;

            if (activity.Attachments != null && activity.Attachments.Any())
            {
                // We know the user is sending an attachment as there is at least one item
                // in the Attachments list.
                reply = await HandleIncomingAttachment(activity);
            }
            else
            {
                // Send at attachment to the user.
                reply = await HandleOutgoingAttachment(turnContext, activity, cancellationToken);
            }

            return reply;
        }

        // Returns a reply with the requested Attachment
        private static async Task<IMessageActivity> HandleOutgoingAttachment(ITurnContext turnContext, IMessageActivity activity, CancellationToken cancellationToken)
        {
            // Look at the user input, and figure out what kind of attachment to send.
            IMessageActivity reply = null;
            if (activity.Text.StartsWith("1"))
            {
                reply = MessageFactory.Text("This is an inline attachment.");
                reply.Attachments = new List<Attachment>() { GetInlineAttachment() };
            }
            else if (activity.Text.StartsWith("2"))
            {
                reply = MessageFactory.Text("This is an attachment from a HTTP URL.");
                reply.Attachments = new List<Attachment>() { GetInternetAttachment() };
            }
            else if (activity.Text.StartsWith("3"))
            {
                reply = MessageFactory.Text("This is an uploaded attachment.");

                // Get the uploaded attachment.
                var uploadedAttachment = await GetUploadedAttachmentAsync(turnContext, activity.ServiceUrl, activity.Conversation.Id, cancellationToken);
                reply.Attachments = new List<Attachment>() { uploadedAttachment };
            }
            else
            {
                // The user did not enter input that this bot was built to handle.
                reply = MessageFactory.Text("Your input was not recognized please try again.");
            }

            return reply;
        }

        // Handle attachments uploaded by users. The bot receives an <see cref="Attachment"/> in an <see cref="Activity"/>.
        // The activity has a "IList{T}" of attachments.    
        // Not all channels allow users to upload files. Some channels have restrictions
        // on file type, size, and other attributes. Consult the documentation for the channel for
        // more information. For example Skype's limits are here
        // <see ref="https://support.skype.com/en/faq/FA34644/skype-file-sharing-file-types-size-and-time-limits"/>.
        private static async Task<IMessageActivity> HandleIncomingAttachment(IMessageActivity activity)
        {
            var replyText = string.Empty;
            foreach (var file in activity.Attachments)
            {
                // Determine where the file is hosted.
                var remoteFileUrl = file.ContentUrl;

                // Save the attachment to the system temp directory.
                var localFileName = Path.Combine(Path.GetTempPath(), file.Name);

                // Download the file from the provided URL
                using (var client = new HttpClient())
                using (var response = await client.GetAsync(remoteFileUrl))
                using (var stream = await response.Content.ReadAsStreamAsync())
                {

                    
                    // Get a reference to the blob container
                    var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
                    await containerClient.CreateIfNotExistsAsync();

                    // Get a reference to the blob where the file will be uploaded
                    var blobClient = containerClient.GetBlobClient(file.Name);

                    // Upload the file to Azure Blob Storage
                    await blobClient.UploadAsync(stream, true);

                    //Extract Content
                     var jobDescriptionText = await _formRecognizerService.ExtractTextFromPdfAsync(remoteFileUrl);

                    //Generate 5 Questions Using AI Service
                    var questions = await _openAIService.GenerateInterviewQuestions(jobDescriptionText);
                    
                    replyText += $"Attachment \"{file.Name}\"" +
                             $" has been received and saved to \"{localFileName}\"\r\n" +
                             // $"{jobDescriptionText}\"\r\n"+
                             $"{questions.ToString()}\"\r\n";
                }


            }

            return MessageFactory.Text(replyText);
        }

        // Creates an inline attachment sent from the bot to the user using a base64 string.
        // Using a base64 string to send an attachment will not work on all channels.
        // Additionally, some channels will only allow certain file types to be sent this way.
        // For example a .png file may work but a .pdf file may not on some channels.
        // Please consult the channel documentation for specifics.
        private static Attachment GetInlineAttachment()
        {
            var imagePath = Path.Combine(Environment.CurrentDirectory, @"Resources", "architecture-resize.png");
            var imageData = Convert.ToBase64String(File.ReadAllBytes(imagePath));

            return new Attachment
            {
                Name = @"Resources\architecture-resize.png",
                ContentType = "image/png",
                ContentUrl = $"data:image/png;base64,{imageData}",
            };
        }

        // Creates an "Attachment" to be sent from the bot to the user from an uploaded file.
        private static async Task<Attachment> GetUploadedAttachmentAsync(ITurnContext turnContext, string serviceUrl, string conversationId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(serviceUrl))
            {
                throw new ArgumentNullException(nameof(serviceUrl));
            }

            if (string.IsNullOrWhiteSpace(conversationId))
            {
                throw new ArgumentNullException(nameof(conversationId));
            }

            var imagePath = Path.Combine(Environment.CurrentDirectory, @"Resources", "architecture-resize.png");

            var connector = turnContext.TurnState.Get<IConnectorClient>() as ConnectorClient;
            var attachments = new Attachments(connector);
            var response = await attachments.Client.Conversations.UploadAttachmentAsync(
                conversationId,
                new AttachmentData
                {
                    Name = @"Resources\architecture-resize.png",
                    OriginalBase64 = File.ReadAllBytes(imagePath),
                    Type = "image/png",
                },
                cancellationToken);

            var attachmentUri = attachments.GetAttachmentUri(response.Id);

            return new Attachment
            {
                Name = @"Resources\architecture-resize.png",
                ContentType = "image/png",
                ContentUrl = attachmentUri,
            };
        }



        // Creates an <see cref="Attachment"/> to be sent from the bot to the user from a HTTP URL.
        private static Attachment GetInternetAttachment()
        {
            // ContentUrl must be HTTPS.
            return new Attachment
            {
                Name = @"Resources\architecture-resize.png",
                ContentType = "image/png",
                ContentUrl = "https://docs.microsoft.com/en-us/bot-framework/media/how-it-works/architecture-resize.png",
            };
        }

        private async Task HandleUserTypeSpecificLogic(ITurnContext<IMessageActivity> turnContext, UserProfile userProfile, CancellationToken cancellationToken)
        {
            switch (userProfile.UserType)
            {
                case "Candidate":
                    await HandleCandidateAsync(turnContext, cancellationToken);
                    break;
                case "HR Assistant":
                    await HandleInterviewerAsync(turnContext, cancellationToken);
                    break;
                case "Administrator":
                   // await HandleAdministratorAsync(turnContext, cancellationToken);
                    break;
                default:
                    await turnContext.SendActivityAsync("Unknown user type.", cancellationToken: cancellationToken);
                    break;
            }
        }


        private async Task HandleCandidateAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            //TODO: Candidate-specific logic Goes here
        }

        private async Task HandleInterviewerAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            // Interviewer-specific logic
            var reply = await ProcessInput(turnContext, cancellationToken);
            //await turnContext.SendActivityAsync("Hello HR Assitant! Would you like to generate interview questions? please upload Post Description here! \"", cancellationToken: cancellationToken);
        }

        private async Task HandleAdministratorAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            // Administrator-specific logic
            await turnContext.SendActivityAsync("Hello Administrator! What administrative task would you like to perform?", cancellationToken: cancellationToken);
        }
    }
}
