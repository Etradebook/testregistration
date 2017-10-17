using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Configuration;

namespace ITechETradeBook_v2.Services
{
    public class Mail
    {
        public void SendMail(string subject, string body, string toAddress, string toName = "NoName")
        {
            string fromAddress = "main@e-tradebook.com";
            string fromName = "e-TradeBook.com";
            //string fromAddress = "cs@e-tradebook.com";
            //string fromName = "e-TradeBook.com";
            string host = "mail.e-tradebook.com";
            int port = 25;
            //int port = 26;
            string userName = ConfigurationManager.AppSettings["SystemEmail"].ToString();
            string password = ConfigurationManager.AppSettings["SystemEmailPassword"].ToString();
            var message = new MailMessage();
            //from, to, reply to
            message.From = new MailAddress(fromAddress, fromName);
            message.To.Add(new MailAddress(toAddress, toName));
            //content
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = true;

            using (var smtpClient = new SmtpClient())
            {
                smtpClient.Host = host;
                smtpClient.Port = port;
                smtpClient.Credentials = new NetworkCredential(userName, password);
                smtpClient.Send(message);
            }
        }

        //public void SendMailCustomAccount(string fromEmail, string pass,string subject, string body, string toAddress, string toName)
        //{
        //    string fromAddress = "cs@e-tradebook.com";
        //    string fromName = "e-TradeBook.com";
        //    string host = "mail.e-tradebook.com";
        //    int port = 26;
        //    string userName = fromEmail;
        //    string password = pass;
        //    var message = new MailMessage();
        //    //from, to, reply to
        //    message.From = new MailAddress(fromAddress, fromName);
        //    message.To.Add(new MailAddress(toAddress, toName));
        //    //content
        //    message.Subject = subject;
        //    message.Body = body;
        //    message.IsBodyHtml = true;

        //    using (var smtpClient = new SmtpClient())
        //    {
        //        smtpClient.Host = host;
        //        smtpClient.Port = port;
        //        smtpClient.Credentials = new NetworkCredential(userName, password);
        //        smtpClient.Send(message);
        //    }
        //}

    }
}