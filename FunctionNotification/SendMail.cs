using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace FunctionNotification
{
    public static class SendMail
    {
        [FunctionName("SendMail")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            // get the request key value pair data
            string keyval = req.Query["keyval"];
            string tolist = req.Query["tolist"];
            string message = req.Query["message"];
            string subject = req.Query["subject"];
            string errmsg = req.Query["errormessage"];

            //log.LogInformation("message - " + message);

            string suggestion = "";

            // if the error message is not blank, propose some appropriate solutions based on the business requirement
            // the proposed solutions can help the team to diagnose the issue
            if (!string.IsNullOrEmpty(errmsg))
            {
                suggestion = GetSuggestedAction(errmsg);
            }

            // get the list of email to lists
            string[] toadd = tolist.Split(',');

            // if the recipient is more than 1
            if (toadd.Length > 1)
            {
                var to_addr = new List<EmailAddress>();

                for (int i = 0; i < toadd.Length; i++)
                {
                    to_addr.Add(new EmailAddress(toadd[i]));
                }

                try
                {
                    ActSendMultiMail(keyval, to_addr, message + suggestion, subject).Wait();
                    log.LogInformation("multi mail sent");
                }
                catch (Exception ex)
                {
                    log.LogInformation("multi error occured - " + ex.Message);
                }
            }
            // if there is only recipient
            else
            {
                try
                {
                    ActSendOneMail(keyval, tolist, message + suggestion, subject).Wait();
                    log.LogInformation("one mail sent");
                }
                catch(Exception ex)
                {
                    log.LogInformation("single error occured - " + ex.Message);
                }
            }

            return new OkResult();
        }

        static string GetSuggestedAction(string errormessage)
        {
            string suggest = "";

            if (errormessage.ToLower().Contains("file is not available"))
            {
                suggest = "<br>Suggested Action: <br>\t\t\t\t 1.Please verify the folder structure of the data source and ensure the structure is correct.";
                suggest = suggest + "<br>\t\t\t\t 2.Please check whether the file is available at the source path. <br>\t\t\t\t 3.Rerun the pipeline once the file is available.";
            }
            else if (errormessage.Contains("System.Data.SqlClient.SqlException"))
            {
                suggest = "<br>Suggested Action: <br>\t\t\t\t 1.Please verify the sql database is available and the required objects exist.";
                suggest = suggest + "<br>\t\t\t\t 2.Rerun the pipeline once the required database objects are available.";
            }
            else if (errormessage.Contains("vaultBaseUrl"))
            {
                suggest = "<br>Suggested Action: <br>\t\t\t\t 1.Please verify the specified key vault is available and the required secrets exist.";
                suggest = suggest + "<br>\t\t\t\t 2.Rerun the pipeline once the key vault and the required secrets are available.";
            }
            else if (errormessage.Contains("RestSourceCallFailed"))
            {
                suggest = "<br>Suggested Action: <br>\t\t\t\t 1.Please verify the data source URL and the credentials are correct.";
                suggest = suggest + "<br>\t\t\t\t 2.Rerun the pipeline after ensuring the data source URL and the credentials are correct.";
            }
            else
            {
                suggest = "<br>Suggested Action: Please contact the technical team to solve the issue.";
            }

            return suggest;
        }

        static async Task ActSendOneMail(string keyval, string tolist, string message,string subject)
        {
            // this code block will send mail to only recipient
            var sendGridClient = new SendGridClient(keyval);
            var from = new EmailAddress("support@testmail.com", "Platform Data Ingestion Status");
            var to = new EmailAddress(tolist);
            var plainContent = "message";
            var htmlContent = message;
            var mailMessage = MailHelper.CreateSingleEmail(from, to, subject, plainContent, htmlContent);


            await sendGridClient.SendEmailAsync(mailMessage);

        }

        static async Task ActSendMultiMail(string keyval, List<EmailAddress> tolist, string message,string subject)
        {
            // this code block will send mail to multiple recipients
            var sendGridClient = new SendGridClient(keyval);
            var mailMessage = new SendGridMessage();
            mailMessage.SetFrom("support@testmail.com", "Platform Data Ingestion Status");
            mailMessage.SetGlobalSubject(subject);

            if (!string.IsNullOrEmpty(message))
            {
                mailMessage.AddContent(MimeType.Html, message);
            }

            mailMessage.AddTos(tolist);

            await sendGridClient.SendEmailAsync(mailMessage);
        }
    }
}
