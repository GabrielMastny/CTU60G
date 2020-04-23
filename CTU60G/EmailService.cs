using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Mail;
using System.IO;
using Newtonsoft.Json;

/****************************************************************************************************  

                                   EXAMPLE HOW TO USE CLASS EmailService
 
***************************************************************************************************/

/*    
    EmailOptions conf = ...

    EmailService email = new EmailService(conf);

    try
    {
        email.Send("Subjects", "Body");
    }
    catch (Exception e)
    {
        // ADD note into log
        Console.WriteLine(e.Message);
    }

*/

/****************************************************************************************************  

                                   STRUCTURE OF SECTION "Email"
 
***************************************************************************************************/

/*    

  {
    "Host": "smtp.gmail.com",                               // GMAIL DEFAULT host 
    "Port": "587",                                          // STANDART SMTP port
    "EnableSsl": "true",                                    // MUST TO BE true, IF NOT send will not work
    "User": "GMAIL MAIL ACOUNT FOR EXAPMLE (testtoscrpts@gmail.com)",
    "Password": "Password of GMAIL MAIL ACOUNT",
    "FromEmail": "EMAIL OF APT (not to be valide)",
    "FromName": "NAMEOFAPT",
    "ToEmails": [                                           // ARRAY OF emails to send msg
      "testtoscrpts@gmail.com",
      "testtoscrpts@gmail.com"
    ]
  }

 */

namespace CTU60G
{
    public class EmailService
    {

        private bool isHTML;
        private MailPriority priority;
        private EmailOptions config;

        public EmailService(EmailOptions conf, MailPriority Priority, bool IsHTLMmsg)
        {
            config = conf;
            isHTML = IsHTLMmsg;
            priority = Priority;
        }
        public EmailService(EmailOptions conf)
        {
            config = conf;
            isHTML = false;
            priority = MailPriority.High;
        }

        private MailMessage createMsg(String subject, String body)
        {
            MailMessage msg = new MailMessage();
            msg.Priority = priority;
            msg.Subject = subject;
            msg.Body = body;
            return msg;
        }

        private static MailAddress createMailAddress(String email, String name)
        {
            return new MailAddress(email, name);
        }



        private void sendEmail(MailMessage msg)
        {

            msg.From = createMailAddress(config.FromEmail, config.FromName);
            foreach (String item in config.ToEmails)
            {
                msg.To.Add(item);
            }

            SmtpClient client = new SmtpClient();
            client.Host = config.Host;
            client.Port = config.Port;
            if (config.EnableSsl == "true")
            {
                client.EnableSsl = true;
            }
            else
            {
                client.EnableSsl = false;
            }
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            client.UseDefaultCredentials = false;
            client.Credentials = new System.Net.NetworkCredential(config.User, config.Password);
            client.Send(msg);
        }

        public void Send(String subject, String body)
        {
            MailMessage msg = createMsg(subject, body);
            sendEmail(msg);
        }

    }
}
