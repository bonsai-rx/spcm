using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Bonsai.Spcm
{
    [Serializable]
    public class SpcmException : Exception
    {
        public SpcmException()
        {
        }

        public SpcmException(string message)
            : base(message)
        {
        }

        public SpcmException(string message, Exception inner)
            : base(message, inner)
        {
        }

        protected SpcmException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
