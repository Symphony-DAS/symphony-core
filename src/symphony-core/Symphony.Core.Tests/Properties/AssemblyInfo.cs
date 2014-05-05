using System.Reflection;
using System.Runtime.InteropServices;
using ApprovalTests.Reporters;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("Symphony.Core.Tests")]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("7e122e42-99e8-4d49-ad9e-1dc60a4d8f5b")]

// Assembly-level preference for ApprovalTests diff reporter
[assembly: UseReporter(typeof(QuietReporter))]