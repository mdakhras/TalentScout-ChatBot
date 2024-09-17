using Azure;
using Azure.Data.Tables;
using System;
using System.Threading.Tasks;

namespace ScoutTalentBot.Model
{
  

    public class InterviewQuestionEntity : ITableEntity
    {
        public string PartitionKey { get; set; } // Could be the job description ID or HR ID
        public string RowKey { get; set; } // Unique identifier, could be a timestamp or UUID
        public string JobDescription { get; set; }
        public string Questions { get; set; }
        public DateTime CreatedDate { get; set; }

        public ETag ETag { get; set; }
        public DateTimeOffset? Timestamp { get; set; }

    }


   
}
