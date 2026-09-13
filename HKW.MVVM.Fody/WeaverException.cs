using System;
using System.Collections.Generic;
using System.Text;

namespace HKW.MVVM.Fody;

public class WeaverException : Exception
{
    public WeaverException(string message)
        : base(message) { }

    public WeaverException(string message, Exception innerException)
        : base(message, innerException) { }
}
