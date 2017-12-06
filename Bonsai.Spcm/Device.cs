using Microsoft.Win32.SafeHandles;
using Spcm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Bonsai.Spcm
{
    public class Device : SafeHandleZeroOrMinusOneIsInvalid
    {
        long memorySize;
        long sampleRate;
        long minSampleRate;
        long maxSampleRate;
        long enabledChannels;
        int bytesPerSample;
        int oversamplingFactor;
        int channelCount;

        public Device(string deviceName)
            : base(true)
        {
            var handle = Drv.spcm_hOpen(deviceName);
            if (handle == IntPtr.Zero)
            {
                ThrowSpcmException(handle);
            }

            SetHandle(handle);
            InitializeCardProperties();
        }

        private void InitializeCardProperties()
        {
            GetParam(Regs.SPC_MIINST_MINADCLOCK, out minSampleRate);
            GetParam(Regs.SPC_MIINST_MAXADCLOCK, out maxSampleRate);
            GetParam(Regs.SPC_MIINST_BYTESPERSAMPLE, out bytesPerSample);
        }

        protected override bool ReleaseHandle()
        {
            Drv.spcm_vClose(handle);
            return true;
        }

        public long SampleRate
        {
            get { return sampleRate; }
        }

        public long MinSampleRate
        {
            get { return minSampleRate; }
        }

        public long MaxSampleRate
        {
            get { return maxSampleRate; }
        }

        public long EnabledChannels
        {
            get { return enabledChannels; }
        }

        public int ChannelCount
        {
            get { return channelCount; }
        }

        public void SetupClockPLL(long sampleRate, bool clockOut = false)
        {
            if (sampleRate > maxSampleRate || sampleRate < minSampleRate)
            {
                throw new ArgumentOutOfRangeException("sampleRate");
            }

            // setup the clock mode
            SetParam(Regs.SPC_CLOCKMODE, Regs.SPC_CM_INTPLL);
            SetParam(Regs.SPC_SAMPLERATE, sampleRate);
            SetParam(Regs.SPC_CLOCKOUT, clockOut ? 1 : 0);
            GetParam(Regs.SPC_SAMPLERATE, out this.sampleRate);
            GetParam(Regs.SPC_OVERSAMPLINGFACTOR, out oversamplingFactor);
        }

        public void SetupSoftwareTrigger(bool triggerOut = false)
        {
            // setup the trigger mode
            SetParam(Regs.SPC_TRIG_ORMASK, Regs.SPC_TMASK_SOFTWARE);
            SetParam(Regs.SPC_TRIG_ANDMASK, 0);
            SetParam(Regs.SPC_TRIG_CH_ORMASK0, 0);
            SetParam(Regs.SPC_TRIG_CH_ORMASK1, 0);
            SetParam(Regs.SPC_TRIG_CH_ANDMASK0, 0);
            SetParam(Regs.SPC_TRIG_CH_ANDMASK1, 0);
            SetParam(Regs.SPC_TRIGGEROUT, triggerOut ? 1 : 0);
        }

        public void SetupRecordFifoSingle(int channelMask, long preTriggerSamples, long segmentSize = 1024, long loops = 0)
        {
            if (segmentSize < 1)
            {
                throw new ArgumentOutOfRangeException("segmentSize");
            }

            // setup the mode
            SetParam(Regs.SPC_CARDMODE, Regs.SPC_REC_FIFO_SINGLE);
            SetParam(Regs.SPC_CHENABLE, channelMask);
            SetParam(Regs.SPC_PRETRIGGER, preTriggerSamples);
            SetParam(Regs.SPC_SEGMENTSIZE, segmentSize);
            SetParam(Regs.SPC_LOOPS, loops);

            // store some information in the structure
            memorySize = 0;
            enabledChannels = channelMask;
            GetParam(Regs.SPC_CHCOUNT, out channelCount);
        }

        public bool WaitDma()
        {
            var errorCode = Drv.spcm_dwSetParam_i32(handle, Regs.SPC_M2CMD, Regs.M2CMD_DATA_WAITDMA);
            if (errorCode == Error.ERR_TIMEOUT) return false;
            if (errorCode != 0)
            {
                ThrowSpcmException(handle);
            }

            return true;
        }

        #region Spcm API

        static void ThrowSpcmException(IntPtr device)
        {
            uint errorReg;
            int errorValue;
            var errorText = new StringBuilder(1024);
            Drv.spcm_dwGetErrorInfo_i32(device, out errorReg, out errorValue, errorText);
            throw new SpcmException(errorText.ToString());
        }

        static void HandleError(IntPtr device, uint errorCode)
        {
            if (errorCode != 0)
            {
                ThrowSpcmException(device);
            }
        }

        public void DefineTransfer(uint bufType, uint direction, uint notifySize, IntPtr buffer, ulong boardOffset, ulong bufferLength)
        {
            var errorCode = Drv.spcm_dwDefTransfer_i64(handle, bufType, direction, notifySize, buffer, boardOffset, bufferLength);
            HandleError(handle, errorCode);
        }

        public void GetContinuousBuffer(uint bufType, out IntPtr buffer, out ulong bufferLength)
        {
            var errorCode = Drv.spcm_dwGetContBuf_i64(handle, bufType, out buffer, out bufferLength);
            HandleError(handle, errorCode);
        }

        public void GetParam(int register, out int value)
        {
            var errorCode = Drv.spcm_dwGetParam_i32(handle, register, out value);
            HandleError(handle, errorCode);
        }

        public void GetParam(int register, out long value)
        {
            var errorCode = Drv.spcm_dwGetParam_i64(handle, register, out value);
            HandleError(handle, errorCode);
        }

        public void SetParam(int register, int value)
        {
            var errorCode = Drv.spcm_dwSetParam_i32(handle, register, value);
            HandleError(handle, errorCode);
        }

        public void SetParam(int register, long value)
        {
            var errorCode = Drv.spcm_dwSetParam_i64(handle, register, value);
            HandleError(handle, errorCode);
        }

        #endregion
    }
}
