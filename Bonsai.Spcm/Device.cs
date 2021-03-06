﻿using Microsoft.Win32.SafeHandles;
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
        long sampleRate;
        long minSampleRate;
        long maxSampleRate;
        long enabledChannels;
        int moduleCount;
        int maxChannels;
        int bytesPerSample;
        int oversamplingFactor;
        int channelCount;
        int cardType;
        CardFunction cardFunction;

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
            GetParam(Regs.SPC_PCITYP, out cardType);
            GetParam(Regs.SPC_MIINST_MINADCLOCK, out minSampleRate);
            GetParam(Regs.SPC_MIINST_MAXADCLOCK, out maxSampleRate);
            GetParam(Regs.SPC_MIINST_MODULES, out moduleCount);
            GetParam(Regs.SPC_MIINST_CHPERMODULE, out maxChannels);
            GetParam(Regs.SPC_MIINST_BYTESPERSAMPLE, out bytesPerSample);

            int fncType;
            GetParam(Regs.SPC_FNCTYPE, out fncType);
            switch (fncType)
            {
                case Regs.SPCM_TYPE_AI: cardFunction = CardFunction.AnalogIn; break;
                case Regs.SPCM_TYPE_AO: cardFunction = CardFunction.AnalogOut; break;
                case Regs.SPCM_TYPE_DI: cardFunction = CardFunction.DigitalIn; break;
                case Regs.SPCM_TYPE_DO: cardFunction = CardFunction.DigitalOut; break;
                case Regs.SPCM_TYPE_DIO: cardFunction = CardFunction.DigitalIO; break;
            }
        }

        protected override bool ReleaseHandle()
        {
            Drv.spcm_vClose(handle);
            return true;
        }

        public int CardType
        {
            get { return cardType; }
        }

        public CardFunction CardFunction
        {
            get { return cardFunction; }
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

        public int ModuleCount
        {
            get { return moduleCount; }
        }

        public int MaxChannels
        {
            get { return maxChannels; }
        }

        public int BytesPerSample
        {
            get { return bytesPerSample; }
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

        public void SetupExternalTrigger(int externalMode, bool triggerTermination = false, int pulseWidth = 0, bool singleTrigger = true, int externalLine = 0)
        {
            // setup the external trigger mode
            SetParam(Regs.SPC_TRIG_EXT0_MODE + externalLine, externalMode);
            SetParam(Regs.SPC_TRIG_TERM, triggerTermination ? 1 : 0);

            // we only use trigout on M2i cards as we otherwise would override the multi purpose i/o lines of M3i
            if ((cardType & global::Spcm.CardType.TYP_SERIESMASK) == global::Spcm.CardType.TYP_M2ISERIES ||
                (cardType & global::Spcm.CardType.TYP_SERIESMASK) == global::Spcm.CardType.TYP_M2IEXPSERIES)
            {
                SetParam(Regs.SPC_TRIG_OUTPUT, 0);
                SetParam(Regs.SPC_TRIG_EXT0_PULSEWIDTH + externalLine, pulseWidth);
            }

            // when singleTrigger is set to true, no other trigger source is used
            if (singleTrigger)
            {
                switch (externalLine)
                {
                    case 0: SetParam(Regs.SPC_TRIG_ORMASK, Regs.SPC_TMASK_EXT0); break;
                    case 1: SetParam(Regs.SPC_TRIG_ORMASK, Regs.SPC_TMASK_EXT1); break;
                    case 2: SetParam(Regs.SPC_TRIG_ORMASK, Regs.SPC_TMASK_EXT2); break;
                }

                SetParam(Regs.SPC_TRIG_ANDMASK, 0);
                SetParam(Regs.SPC_TRIG_CH_ORMASK0, 0);
                SetParam(Regs.SPC_TRIG_CH_ORMASK1, 0);
                SetParam(Regs.SPC_TRIG_CH_ANDMASK0, 0);
                SetParam(Regs.SPC_TRIG_CH_ANDMASK1, 0);
            }

            // M3i cards need trigger level to be programmed for Ext0 = analog trigger
            if ((cardType & global::Spcm.CardType.TYP_SERIESMASK) == global::Spcm.CardType.TYP_M3ISERIES ||
                (cardType & global::Spcm.CardType.TYP_SERIESMASK) == global::Spcm.CardType.TYP_M3IEXPSERIES)
            {
                if (externalLine == 0)
                {
                    SetParam(Regs.SPC_TRIG_EXT0_LEVEL0, 1500); // 1500 mV
                    SetParam(Regs.SPC_TRIG_EXT0_LEVEL1, 800); // 800 mV (rearm)
                }
            }
            // M4i/M4x cards need trigger level to be programmed for Ext0 or Ext1
            else if ((cardType & global::Spcm.CardType.TYP_SERIESMASK) == global::Spcm.CardType.TYP_M4IEXPSERIES ||
                     (cardType & global::Spcm.CardType.TYP_SERIESMASK) == global::Spcm.CardType.TYP_M4XEXPSERIES)
            {
                if (externalLine == 0)
                {
                    SetParam(Regs.SPC_TRIG_EXT0_LEVEL0, 1500); // 1500 mV
                    SetParam(Regs.SPC_TRIG_EXT0_LEVEL1, 800); // 800 mV (rearm)
                    SetParam(Regs.SPC_TRIG_EXT0_ACDC, 0); // DC coupling
                }
                else if (externalLine == 1)
                {
                    SetParam(Regs.SPC_TRIG_EXT1_LEVEL0, 1500); // 1500 mV
                    SetParam(Regs.SPC_TRIG_EXT1_ACDC, 0); // DC coupling
                }
            }
        }

        public void SetupChannelTrigger(
            int channel,
            int triggerMode,
            int triggerLevel0 = 0,
            int triggerLevel1 = 0,
            int pulseWidth = 0,
            bool triggerOut = false,
            bool singleTrigger = true)
        {
            if ((channel < 0) || (channel >= maxChannels))
            {
                throw new ArgumentOutOfRangeException("channel");
            }

            SetParam(Regs.SPC_TRIG_CH0_MODE + channel, triggerMode);
            SetParam(Regs.SPC_TRIG_CH0_PULSEWIDTH + channel, pulseWidth);

            if (cardFunction == CardFunction.AnalogIn)
            {
                SetParam(Regs.SPC_TRIG_CH0_LEVEL0 + channel, triggerLevel0);
                SetParam(Regs.SPC_TRIG_CH0_LEVEL1 + channel, triggerLevel1);
            }

            // we only use trigout on M2i cards as we otherwise would override the multi purpose i/o lines of M3i
            if ((cardType & global::Spcm.CardType.TYP_SERIESMASK) == global::Spcm.CardType.TYP_M2ISERIES ||
                (cardType & global::Spcm.CardType.TYP_SERIESMASK) == global::Spcm.CardType.TYP_M2IEXPSERIES)
            {
                SetParam(Regs.SPC_TRIG_OUTPUT, triggerOut ? 1 : 0);
            }

            SetParam(Regs.SPC_TRIG_TERM, 0);

            // when singleTrigger is set to true, no other trigger source is used
            if (singleTrigger)
            {
                SetParam(Regs.SPC_TRIG_ORMASK, 0);
                SetParam(Regs.SPC_TRIG_ANDMASK, 0);
                SetParam(Regs.SPC_TRIG_CH_ORMASK1, 0);
                SetParam(Regs.SPC_TRIG_CH_ANDMASK1, 0);

                // some cards need the and mask to use on pulsewidth mode -> to be sure we set the AND mask for all pulsewidth cards
                if ((triggerMode & Regs.SPC_TM_PW_GREATER) != 0 || (triggerMode & Regs.SPC_TM_PW_SMALLER) != 0)
                {
                    SetParam(Regs.SPC_TRIG_CH_ORMASK0, 0);
                    SetParam(Regs.SPC_TRIG_CH_ANDMASK0, 1 << channel);
                }
                else
                {
                    SetParam(Regs.SPC_TRIG_CH_ORMASK0, 1 << channel);
                    SetParam(Regs.SPC_TRIG_CH_ANDMASK0, 0);
                }
            }
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
            enabledChannels = channelMask;
            GetParam(Regs.SPC_CHCOUNT, out channelCount);
        }

        public void SetupRecordFifoMulti(int channelMask, long segmentSize = 1024, long postTriggerSamples = 512, long loops = 0)
        {
            if (segmentSize < 1)
            {
                throw new ArgumentOutOfRangeException("segmentSize");
            }

            // setup the mode
            SetParam(Regs.SPC_CARDMODE, Regs.SPC_REC_FIFO_MULTI);
            SetParam(Regs.SPC_CHENABLE, channelMask);
            SetParam(Regs.SPC_SEGMENTSIZE, segmentSize);
            SetParam(Regs.SPC_POSTTRIGGER, postTriggerSamples);
            SetParam(Regs.SPC_LOOPS, loops);

            // store some information in the structure
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
