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

        void AddNote(String text);
    }

    public interface IExperimentData : IDataObject
    {
        String Purpose { get; }

        DateTimeOffset StartTime { get; }
    }
}
