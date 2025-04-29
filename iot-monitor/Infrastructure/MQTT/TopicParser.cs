using System.Text.RegularExpressions;

namespace iot_monitor.Infrastructure.MQTT
{
    public class TopicParser
    {
        /// <summary>
        /// Convert string template to get id from path
        /// </summary>
        /// <param name="template">Template topic has the target {id}, ie. "rsu/AccidentReport/{id}"</param>
        /// <param name="topic">Topic  desired to be extracted</param>
        /// <returns>Return {id} value if matched, otherwise return null</returns>
        public static string? ExtractId(string template, string topic)
        {
            var patternParts = template.Split('/');
            var topicParts = topic.Split('/');

            if (patternParts.Length != topicParts.Length)
            {
                return null;
            }

            for (int i = 0; i < patternParts.Length; i++)
            {
                if (!patternParts[i].StartsWith("{") || !patternParts[i].EndsWith("}"))
                {
                    continue;
                }
                return topicParts[i];
            }

            return null;
        }
    }
}