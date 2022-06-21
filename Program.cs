// See https://aka.ms/new-console-template for more information
using System.Net;
using System.IO;
using System.Text;
using Newtonsoft.Json;


namespace SolteqJsonServer
{

    public class Program
    {
        public static HttpListener listener;

        public static async Task HandleIncomingConnections()
        {
            while (true)
            {

                HttpListenerContext ctx = await listener.GetContextAsync();
                HttpListenerRequest req = ctx.Request;
                HttpListenerResponse resp = ctx.Response;

                //print data of user
                Console.WriteLine("Connection: " + req.UserAgent);
                Console.WriteLine("IP: " + req.RemoteEndPoint.ToString());
                Console.WriteLine();

                string returnableData = getDataFromAPI();

                byte[] data = Encoding.UTF8.GetBytes(String.Format(returnableData));

                resp.ContentType = "text/html";
                resp.AppendHeader("Access-Control-Allow-Origin", "*");
                resp.ContentEncoding = Encoding.UTF8;
                resp.ContentLength64 = data.LongLength;

                await resp.OutputStream.WriteAsync(data, 0, data.Length);
                resp.Close();
            }
        }
        public static async void writeFile(string filename, string[] lines)
        {
            filename += "-" + DateTime.Now.ToShortDateString() + ".csv";
            filename = filename.Replace("/", "-");

            //need to replace every slash with a dash because windows will think of anything before a slash as a directory
            await File.WriteAllLinesAsync(filename, lines);
        }

        public static string getDataFromAPI()
        {

            string url = "https://helsinki-openapi.nuuka.cloud/api/v1.0/EnergyData/Daily/ListByProperty?Record=LocationName&SearchString=1000%20Hakaniemen%20kauppahalli&ReportingGroup=Electricity&StartTime=2019-01-01&EndTime=2019-12-31";
            string json;
            HttpClient jsonReader = new HttpClient();

            using(HttpResponseMessage response = jsonReader.GetAsync(url).Result)
            {
                using(HttpContent content = response.Content)
                {
                    json = content.ReadAsStringAsync().Result; 
                }
            }

            //array for each month, contains total kwh used per day of month
            float[] kwhCountPerMonth = new float[12];

            string[] months = { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
            List<string> days = new List<string>();
            //making a list of days, the list of days needs to have dynamic memory (not array) since there can be a very long list of days queried

            dynamic data = JsonConvert.DeserializeObject(json);

            for( int i = 0; i < data.Count; i++)
            {
                dynamic item = data[i];
                string timestamp = (string)item.timestamp;
                int monthIndex = int.Parse(timestamp.Substring(0,2)); //the month is the first 2 letters of timestamp string

                string value = (string)item.value;
                kwhCountPerMonth[monthIndex-1] += float.Parse(value); //adds kwh used during the day in question to the total used that month

                //adding the needed info of the day to the days list
                days.Add((string)item.timestamp + "," + (string)item.value);
            }

            using(TextWriter tw = new StreamWriter("kwhPerDay-" + DateTime.Now.ToShortDateString().Replace("/","-") + ".csv")) //writing a file of how many kwh was used per day of query
            {
                foreach(string s in days)
                    tw.WriteLine(s);
            }

            string html = "";
            for (int i = 0; i < months.Length; i++) // making a html list of info to be displayed on front end
            {
                html += "<div class=\"monthData\"> <p class=\"monthText\">" +  months[i] + "</p> <p class=\"kwhText\">kwh: " + (int)kwhCountPerMonth[i] + "</p> </div>";
                months[i] += "," + kwhCountPerMonth[i];
            }
            writeFile("kwhPerMonth",months);
            return html;
        }

        public static void Main()
        {
            listener = new HttpListener();
            //requires elevated user perms to run server!!
            listener.Prefixes.Add("http://+:7777/");
            listener.Start();

            Task listenTask = HandleIncomingConnections();
            listenTask.GetAwaiter().GetResult();

            listener.Close();

        }
    }
}
