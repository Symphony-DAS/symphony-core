using System;
using System.Runtime.InteropServices;

namespace Heka.NativeInterop
{
    public class ErrorDescription
    {

        public static string ErrorString(uint hekkaError)
        {
            byte[] text = new byte[200];
            int size = text.Length;

            IntPtr textPtr = Marshal.AllocHGlobal(size);

            string result = null;
            try
            {
                ITCMM.ITC_AnalyzeError((int)hekkaError, textPtr, (uint)size);

                Marshal.Copy(textPtr, text, 0, size);

                int end = Array.FindIndex(text, 0, (x) => x == 0);
                if (end == -1)
                {
                    end = size;
                }

                System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();

                result = enc.GetString(text, 0, end);
            }
            finally
            {
                Marshal.FreeHGlobal(textPtr);
            }

            return result;
        }

    }
}
