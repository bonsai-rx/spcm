using OpenCV.Net;
using Spcm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bonsai.Spcm
{
    public class FifoAcquisition : Source<Mat>
    {
        public FifoAcquisition()
        {
            DeviceName = "/dev/spcm0";
            PreTriggerSamples = 1024;
            SampleRate = 200000000;
            BufferSize = 1024 * 1024 * 160; //MB
            NotifySize = 1024 * 1024 * 10; //10 MB chunks
        }

        public string DeviceName { get; set; }

        public int SampleRate { get; set; }

        public int PreTriggerSamples { get; set; }

        public int BufferSize { get; set; }

        public int NotifySize { get; set; }

        public override IObservable<Mat> Generate()
        {
            return Observable.Create<Mat>((observer, cancellationToken) =>
            {
                return Task.Factory.StartNew(() =>
                {
                    using (var device = new Device(DeviceName))
                    {
                        //var channelMask = -1; // 32 ch
                        var channelMask = 255; // 8 ch

                        device.SetupRecordFifoSingle(channelMask, PreTriggerSamples);
                        device.SetupClockPLL(SampleRate);
                        device.SetupSoftwareTrigger();

                        IntPtr contBuffer;
                        ulong contBufferLength;
                        device.GetContinuousBuffer(Drv.SPCM_BUF_DATA, out contBuffer, out contBufferLength);

                        // define buffer transfers
                        var dataNotify = NotifySize;
                        var dataLength = BufferSize; //mB
                        using (var dataBuffer = new Mat(1, dataLength, Depth.U8, 1))
                        {
                            device.DefineTransfer(Drv.SPCM_BUF_DATA, Drv.SPCM_DIR_CARDTOPC, (uint)dataNotify, dataBuffer.Data, 0, (ulong)dataBuffer.Step);

                            // start card and transfers
                            var timeout = 100;
                            var startCommand = Regs.M2CMD_CARD_START | Regs.M2CMD_CARD_ENABLETRIGGER | Regs.M2CMD_DATA_STARTDMA;
                            //var waitCommand = Regs.M2CMD_DATA_WAITDMA;
                            device.SetParam(Regs.SPC_TIMEOUT, timeout);
                            device.SetParam(Regs.SPC_M2CMD, startCommand);

                            // loop
                            int dataAvailableBytes;
                            int dataAvailableOffset;
                            while (!cancellationToken.IsCancellationRequested)
                            {
                                if (!device.WaitDma()) continue;

                                int status;
                                device.GetParam(Regs.SPC_M2STATUS, out status);
                                device.GetParam(Regs.SPC_DATA_AVAIL_USER_LEN, out dataAvailableBytes);
                                device.GetParam(Regs.SPC_DATA_AVAIL_USER_POS, out dataAvailableOffset);
                                if (dataAvailableBytes >= dataLength)
                                {
                                    throw new InvalidOperationException("Overrun!!");
                                }

                                if ((dataAvailableOffset + dataAvailableBytes) >= dataLength)
                                {
                                    // process data only up to the end of the buffer
                                    dataAvailableBytes = dataLength - dataAvailableOffset;
                                }

                                var dataAvailable = dataBuffer.GetSubRect(new Rect(dataAvailableOffset, 0, dataAvailableBytes, 1)).Clone();
                                device.SetParam(Regs.SPC_DATA_AVAIL_CARD_LEN, dataAvailableBytes);
                                observer.OnNext(dataAvailable);
                            }
                        }
                    }
                },
                cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            });
        }
    }
}
