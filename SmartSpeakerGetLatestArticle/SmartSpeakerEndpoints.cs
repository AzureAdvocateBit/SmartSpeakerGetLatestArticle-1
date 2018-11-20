﻿
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using CEK.CSharp;
using CEK.CSharp.Models;
using TechSummit2018.ServerlessSmartSpeaker.Services;
using Google.Protobuf;
using Google.Cloud.Dialogflow.V2;
using Google.Protobuf.WellKnownTypes;
using Alexa.NET.Request;
using Alexa.NET.Request.Type;
using Alexa.NET.Response;

namespace TechSummit2018.ServerlessSmartSpeaker
{
    public static class SmartSpeakerEndpoints
    {
        private static string IntroductionMessage { get; } = "こんにちは、LINEデベロッパー・デイのデモアプリです。最新記事を教えてと聞いてください。";
        private static string HelloMessage { get; } = "こんにちは、ちょまどさん！";
        private static string ErrorMessage { get; } = "すみません、わかりませんでした！";

        [FunctionName("Line")]
        public static async Task<IActionResult> Line([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)]HttpRequest req, ILogger log)
        {
            var client = new ClovaClient();
            var cekRequest = await client.GetRequest(req.Headers["SignatureCEK"], req.Body);
            var cekResponse = new CEKResponse();
            switch (cekRequest.Request.Type)
            {
                case RequestType.LaunchRequest:
                    cekResponse.AddText(IntroductionMessage);
                    cekResponse.ShouldEndSession = false;
                    break;
                case RequestType.IntentRequest:
                    cekResponse.AddText(await HandleIntentAsync(cekRequest.Request.Intent.Name));
                    break;
            }

            return new OkObjectResult(cekResponse);
        }


        [FunctionName("GoogleHome")]
        public static async Task<IActionResult> GoogleHome([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)]HttpRequest req, ILogger log)
        {
            var parser = new JsonParser(JsonParser.Settings.Default.WithIgnoreUnknownFields(true));
            var webhookRequest = parser.Parse<WebhookRequest>(await req.ReadAsStringAsync());
            var webhookResponse = new WebhookResponse();
            log.LogInformation(webhookRequest.QueryResult.Intent.DisplayName);
            switch (webhookRequest.QueryResult.Intent.DisplayName)
            {
                case "Default Welcome Intent":
                    webhookResponse.FulfillmentText = IntroductionMessage;
                    break;
                default:
                    webhookResponse.FulfillmentText = await HandleIntentAsync(webhookRequest.QueryResult.Intent.DisplayName);
                    break;
            }

            return new ProtcolBufJsonResult(webhookResponse, JsonFormatter.Default);
        }

        [FunctionName("Alexa")]
        public static async Task<IActionResult> Alexa([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)]HttpRequest req, ILogger log)
        {
            var skillRequest = JsonConvert.DeserializeObject<SkillRequest>(await new StreamReader(req.Body).ReadToEndAsync());
            var skillResponse = new SkillResponse
            {
                Version = "1.0",
                Response = new ResponseBody(),
            };
            switch (skillRequest.Request)
            {
                case LaunchRequest lr:
                    skillResponse.Response.OutputSpeech = new PlainTextOutputSpeech
                    {
                        Text = IntroductionMessage,
                    };
                    break;
                case IntentRequest ir:
                    skillResponse.Response.OutputSpeech = new PlainTextOutputSpeech
                    {
                        Text = await HandleIntentAsync(ir.Intent.Name),
                    };
                    break;
                default:
                    skillResponse.Response.OutputSpeech = new PlainTextOutputSpeech
                    {
                        Text = "すいません。わかりません。",
                    };
                    break;
            }

            return new OkObjectResult(skillResponse);
        }

        private static async Task<string> HandleIntentAsync(string intent)
        {
            switch (intent)
            {
                case "HelloIntent":
                    return HelloMessage;
                case "AskLatestBlogTitleIntent":
                    {
                        var chomadoBlogService = new ChomadoBlogService();
                        var title = await chomadoBlogService.GetLatestBlogTitleAsync();

                        if (!string.IsNullOrEmpty(title))
                        {
                            return $"ちょまどさんのブログの最新記事は {title} です。";
                        }
                        else
                        {
                            return "ちょまどさんのブログの最新記事は、わかりませんでした。";
                        }
                    }
                default:
                    return ErrorMessage;
            }
            
        }
    }
}
