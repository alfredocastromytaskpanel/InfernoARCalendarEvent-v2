using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ToDoListService.Extensions;

namespace ToDoListService.Models
{
    public class InfernoEvent
    {
        public string id { get; set; }

        public string name { get; set; }

        public string description { get; set; }

        public string clientId { get; set; }

        public DateTimeOffset preRoll { get; set; }

        public DateTimeOffset startTime { get; set; }

        public Microsoft.Graph.Event ToMSGraphEvent()
        {
            var tzName = startTime.GetTimeZoneStandardName();

            var newEvent = new Event
            {
                Subject = name, //"Let's go for lunch"
                Body = new ItemBody
                {
                    ContentType = BodyType.Html,
                    Content = name + Environment.NewLine + description //"Does noon work for you?"
                },
                Start = new DateTimeTimeZone
                {
                    DateTime = startTime.DateTime.ToString("yyyy-MM-ddTHH:mm:ss"), //"yyyy'-'MM'-'dd'T'HH':'mm':'ss" "2021-03-28T12:00:00",
                    TimeZone = tzName //"Pacific Standard Time"
                },
                End = new DateTimeTimeZone
                {
                    DateTime = startTime.DateTime.ToString("yyyy-MM-ddTHH:mm:ss"), //"2021-03-28T14:00:00",
                    TimeZone = tzName //"Pacific Standard Time"
                },
                //Location = new Location
                //{
                //    DisplayName = "Harry's Bar"
                //},
                //Attendees = new List<Attendee>()
                //{
                //    new Attendee
                //    {
                //        EmailAddress = new EmailAddress
                //        {
                //            Address = recipients,
                //            Name = "Alfredo Castro"
                //        },
                //        Type = AttendeeType.Required
                //    }
                //},
            };
            return newEvent;
        }
    }
}
