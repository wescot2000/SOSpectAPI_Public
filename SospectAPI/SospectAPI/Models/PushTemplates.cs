using System;
namespace SospectAPI.Models
{
    public class PushTemplates
    {
        public class Generic
        {
            public const string Android = "{ \"data\" : {\"message\" : \"$(alertMessage)\", \"action\" : \"$(alertAction)\", \"alarma_id\" : \"$(alarma_id)\"} }";

            public const string iOS = "{ \"aps\" : {\"alert\" : { \"title\" : \"SOSpect\", \"body\" : \"$(alertMessage)\"}, \"badge\" : 1 }, \"action\" : \"$(alertAction)\" , \"alarma_id\" : \"$(alarma_id)\"}";
        }

        //public class Silent
        //{
        //    public const string Android = "{ \"data\" : {\"message\" : \"$(alertMessage)\", \"action\" : \"$(alertAction)\", \"alarma_id\" : \"$(alarma_id)\"} }";
        //    public const string iOS = "{ \"aps\" : {\"content-available\" : 1, \"apns-priority\": 5, \"sound\" : \"\", \"badge\" : 1}, \"message\" : \"$(alertMessage)\", \"action\" : \"$(alertAction)\", \"alarma_id\" : \"$(alarma_id)\" }";
        //}
    }
}


