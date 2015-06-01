using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Symphony.Core
{
    public interface IDocument
    {
        IExperimentData Experiment { get; }
    }

    public interface IDataObject
    {
        IDictionary<String, Object> Properties { get; } 

        void AddProperty(String key, Object value);

        IEnumerable<Note> Notes { get; } 

        void AddNote(String text);
    }

    public interface IExperimentData : IDataObject
    {
        String Purpose { get; }

        DateTimeOffset StartTime { get; }
    }

    public class Note
    {
        public String Text { get; private set; }

        public DateTimeOffset Time { get; private set; }

        public Note(DateTimeOffset time, String text)
        {
            Time = time;
            Text = text;
        }
    }
}
