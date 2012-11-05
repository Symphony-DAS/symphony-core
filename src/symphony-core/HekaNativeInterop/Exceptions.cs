using System;

namespace Heka
{
    using Heka.NativeInterop;
    using Symphony.Core;

    public class HekaDAQException : DAQException
    {
        public string HekaError { get; private set; }
        public uint HekaErrorCode { get; private set; }

        public override string Message
        {
            get
            {
                if (HekaErrorCode == 0)
                    return base.Message + " (" + HekaError + ")";

                return base.Message + " (" + HekaError + "; 0x" + HekaErrorCode.ToString("X") + ")";
            }
        }
        public HekaDAQException(string msg) : this(msg, 0) { }

        public HekaDAQException(string msg, uint errorCode) :
            base(msg)
        {
            HekaError = ErrorDescription.ErrorString(errorCode);
            HekaErrorCode = errorCode;
        }

    }

    public class HekaDAQBufferUnderrunException : ApplicationException
    {
        public HekaDAQBufferUnderrunException(string msg) : base(msg) { }
        public HekaDAQBufferUnderrunException() : base() { }
    }
}
