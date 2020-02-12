using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using System.Threading.Tasks;           //Add to process Async Task
using Microsoft.Bot.Connector;          //Add for Activity Class
using Microsoft.Bot.Builder.Dialogs;    //Add for Dialog Class
using System.Threading;                 
using QnAMakerDialog.Models;            //Add for reference QnAMakerDialog  
using QnAMakerDialog;                   //Add for reference QnAMakerDialog  

namespace GreatWall
{
    [Serializable]

    //[QnAMakerService(Host, EndpointKey, Knowledgebases, MaxAnswers = 0)]
    [QnAMakerService("https://greatwallqna.azurewebsites.net/qnamaker",
     "740f5c87-bd86-49a6-871f-4af1ef992e14", "d42882ec-8351-4c87-8381-29a64c4c8d2e", 
      MaxAnswers = 5)]

    public class FAQDialog : QnAMakerDialog<string>  //Inheritance from QnAMakerDialog
    {
        //This method is called automatically when there are no results for the question.
        public override async Task NoMatchHandler(IDialogContext context, 
                                                  string originalQueryText)
        {
            await context.PostAsync($"Sorry, I couldn't find an answer for '{originalQueryText}'.");

            context.Wait(MessageReceived);
        }

        //This method is called automatically when there is a result for the question.
        public override async Task DefaultMatchHandler(IDialogContext context, 
                                   string originalQueryText, QnAMakerResult result)
        {
            if (originalQueryText == "Exit")
            {
                context.Done("");
                return;
            }
            await context.PostAsync(result.Answers.First().Answer);

            context.Wait(MessageReceived);
        }

        [QnAMakerResponseHandler(0.5)]  //1: 100%, 0.5: 50%
        //This method is called when there is a low-order result.
        public async Task LowScoreHandler(IDialogContext context, string originalQueryText, 
                                          QnAMakerResult result)
        {
            var messageActivity = ProcessResultAndCreateMessageActivity(context, ref result);

            messageActivity.Text = $"I found an answer that might help..." +
                                   $"{result.Answers.First().Answer}.";

            await context.PostAsync(messageActivity);

            context.Wait(MessageReceived);
        }
    }
}