using System;
using System.Diagnostics;
using System.Threading;
using System.Linq;
//using NationalInstruments.VisaNS;
//using NationalInstruments.VisaNS;
using System.Runtime.Remoting.Messaging;
using System.Collections.Generic;
using System.Text;
using Ivi.Visa.Interop;


namespace Examples.AdvancedProgramming.AsynchronousOperations
{
    public struct Fraction
    {
        public static int getGCD(double a, double b)
        {
            //Drop negative signs
            a = Math.Abs(a);
            b = Math.Abs(b);

            //Return the greatest common denominator between two integers
            while (a != 0 && b != 0)
            {
                if (a > b)
                    a %= b;
                else
                    b %= a;
            }

            if (a == 0)
                return (int)b;
            else
                return (int)a;
        }

        public static int getLCD(int a, int b)
        {
            //Return the Least Common Denominator between two integers
            return (a * b) / getGCD(a, b);
        }
    }
    // Create a class that simulates sampling .
    public class sampleCollector
    {
        public static short[] getSamples(int numSamples, ref double sampleRate, ref int AWGfreq)
        {
            //USB buffer of int16 values
            short[] buffer = new short[numSamples];
            //scale amplitude to max int16 value
            double amplitude = short.MaxValue;
            //create sinewave data
            for (int n = 0; n < buffer.Length; n++)
            {
                buffer[n] = (short)(amplitude * Math.Sin((2 * Math.PI * n * AWGfreq) / sampleRate));
            }
            return buffer;
        }
    }

    // Create an asynchronous delegate that matches the Factorize method.
    public delegate short[] AsyncSampleCaller(int numSamples, ref double sampleRate,ref int AWGfreq);
    public class DemonstrateAsyncPattern
    {
        public const int SAMPLE_RATE = 10000001;
        public const int PRECISION = 15;
        public Stopwatch stopWatch = new Stopwatch();
        // The waiter object used to keep the main application thread
        // from terminating before the callback method completes.
        ManualResetEvent waiter;
        public List<int> calcTimeArrays(double time, double AWGFreq, double sampFreq, int numSamples, out double[] realSampTimes, out double[] relativeSampTimes)
        {
            //beginning of most recent period Rt0n
            double relativeT0 = 0;
            //sample times of each sample in the buffer tsn
            realSampTimes = new double[numSamples];
            //sample times relative to calculated beginning of most recent period Rtsn
            relativeSampTimes = new double[numSamples];
            //for each sample in the buffer n find Rt0n and Rtsn
            for (int n = 0; n < realSampTimes.Length; n++)
            {
                //calculate tsn for each sample in the buffer n 
                realSampTimes[n] = Math.Round((double)(n) * (((double)1 / sampFreq)), PRECISION, MidpointRounding.AwayFromZero);
                //calculate Rt0n for each sample in the buffer n 
                int numerator = (int)(realSampTimes[n] / (1 / AWGFreq));
                if (n == 0) { relativeT0 = 0; }
                else { relativeT0 = Math.Round(((double)(numerator) / AWGFreq), PRECISION, MidpointRounding.AwayFromZero); }
                //calculate Rtsn for each sample in the buffer n
                if (n == 0) { relativeSampTimes[n] = 0; }
                else { relativeSampTimes[n] = Math.Round(realSampTimes[n] - relativeT0, PRECISION, MidpointRounding.AwayFromZero); }
            }
            //order samples by Rtsn and return sampleOrder
            List<double> relSampTimes = relativeSampTimes.OfType<double>().ToList();
            var sorted = relSampTimes
                .Select((x, i) => new KeyValuePair<double, int>(x, i))
                .OrderBy(x => x.Key)
                .ToList();
            List<double> sortedSamples = sorted.Select(x => x.Key).ToList();
            List<int> sampleOrder = sorted.Select(x => x.Value).ToList();
            return sampleOrder;


        }
        public int CalcNumSamples(int AWGFreq, out double EquSampRate, out double time2complete)
        {
            int numSamples = 0;
            double tOne = (double)(1 / (double)AWGFreq);
            //double tTwo = (double)(1 / (double)SAMPLE_RATE);
            int fCoincidence = Fraction.getGCD(AWGFreq, SAMPLE_RATE);
            double tCoincidence = 1 / (double)fCoincidence;
            numSamples = (int)(tCoincidence * SAMPLE_RATE);
            time2complete = tCoincidence;
            EquSampRate = (double)AWGFreq*(double)numSamples;
            return numSamples;
        }
        public void PrintError(int AWGFreq)
        {
            System.Console.WriteLine("Usage: Calibrate <AWG_Freq (Hz)>");
            System.Console.WriteLine("AWG Frequency: {0}", AWGFreq);
            System.Console.WriteLine("Real ADC Sample Rate: {0}", SAMPLE_RATE);
            System.Console.WriteLine("Equivalent Sampling Rate: N/A ");
            System.Console.WriteLine("Number of Samples: N/A");
        }

        // Define the method that receives a callback when the results are available.
        public void ProcessSamples(IAsyncResult result)
        {
            double sampleRate = 0;
            int AWGfreq = 0;
            // Extract the delegate from the 
            // System.Runtime.Remoting.Messaging.AsyncResult.
            AsyncSampleCaller sampleDelegate = (AsyncSampleCaller)((AsyncResult)result).AsyncDelegate;

            /*TYPE CASTING*/
            int numSamples = (int)result.AsyncState;
            double time = 0;
            double[] realSampTimes = new double[numSamples];
            double[] relativeSampTimes = new double[numSamples];
            short[] orderedSamples = new short[numSamples];
            // Obtain the result.
            short[] buffer = new short[numSamples];
            buffer = sampleDelegate.EndInvoke(ref sampleRate, ref AWGfreq, result);
            //end waveform capture time 
            this.stopWatch.Stop();
            TimeSpan ts = this.stopWatch.Elapsed;
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            System.Console.WriteLine("Waveform capture complete. Run time: "+elapsedTime);
            System.Console.WriteLine(@"Processing data and creating plots with matlab");
            //Begin data processing 
            //calculate sample order
            List<int> sampleOrder = calcTimeArrays(time, AWGfreq, sampleRate, numSamples, out realSampTimes, out relativeSampTimes);
            //order samples 
            for (var i = 0; i < sampleOrder.Count; i++)
            {
                // Console.WriteLine("Index is {0} and value is {1}", sampleOrder[i], buffer[sampleOrder[i]]);
                orderedSamples[i] = buffer[sampleOrder[i]];

            }
            // Create the MATLAB instance 
            // Change to the directory where the function is located 
            MLApp.MLApp matlab = new MLApp.MLApp();
             
          
             matlab.Execute(@"cd c:\temp\");

             // Define the output 
             object result2 = null;

             // Call the MATLAB function myfunc
             matlab.Feval("myfunc", 2, out result2, orderedSamples, sampleRate);

             // Display result 
             object[] res = result2 as object[];

             //Console.WriteLine(res[0]);
             //Console.WriteLine(res[1]);
            System.Console.WriteLine("AWG Frequency: {0}Hz", AWGfreq);
            System.Console.WriteLine("Real ADC Sample Rate: {0}Hz", SAMPLE_RATE);
            System.Console.WriteLine("Equivalent Sampling Rate: {0}Hz ",sampleRate);
            System.Console.WriteLine("Number of Samples: {0}",numSamples);
            System.Console.WriteLine("SFDR: {0}dB",res[0]);
            System.Console.WriteLine("\nDone processing, see matlab for plots. Enter 'e' to exit.");
            string line = Console.ReadLine();
            if (line == "e")
            {
                Environment.Exit(0);
            }
            else
            {
                System.Console.WriteLine("Invalid input, please enter 'e'. Exiting with code 1");
                Environment.Exit(1);
            }
            waiter.Set();
        }

        // The following method demonstrates the asynchronous pattern using a callback method.
        public void GetSamplesUsingCallback(int numSamples, double sampleRate, int AWGfreq)
        {
            
            AsyncSampleCaller sampleDelegate = new AsyncSampleCaller(sampleCollector.getSamples);
            //int temp = 0;
            // Waiter will keep the main application thread from 
            // ending before the callback completes because
            // the main thread blocks until the waiter is signaled
            // in the callback.
            waiter = new ManualResetEvent(false);

            // Define the AsyncCallback delegate.
            AsyncCallback callBack = new AsyncCallback(this.ProcessSamples);

            // Asynchronously invoke the Factorize method.
            IAsyncResult result = sampleDelegate.BeginInvoke(
                                 numSamples,
                                 ref sampleRate,
                                 ref AWGfreq,
                                 callBack,
                                 numSamples);

            // Do some other useful work while 
            // waiting for the asynchronous operation to complete.

            // When no more work can be done, wait.
            waiter.WaitOne();
        }
        public int calibrateAWG(int AWGFreq)
        {
            double EquSampRate = 0;
            double time = 0;
            //AWGFreq = 100000000;
            System.Console.WriteLine("Please configure unit to output waveform with freqeucncy specified.\nBegin waveform capture? (y/n)");
            string line = Console.ReadLine();
            if (line == "y")
            {
                DemonstrateAsyncPattern demonstrator = new DemonstrateAsyncPattern();
                demonstrator.stopWatch.Start();
                int numSamples = demonstrator.CalcNumSamples(AWGFreq, out EquSampRate, out time);
                int timeMs = (int)(time * 1000);
                //sleep amount of time to collect samples calculated by CalcNumSamples and sleep thread 
                //for that amount of time to simulate real waveform capture
                Thread.Sleep(timeMs);
                /* ResourceManager rMgr = new ResourceManager();
                 FormattedIO488 src = new FormattedIO488();
                 string srcAddress = "USB::0x0699::0x0343::C025388::0";
                 src.IO = (Ivi.Visa.Interop.IMessage)rMgr.Open(srcAddress, AccessMode.NO_LOCK, 2000, null);
                 src.IO.Timeout = 2000;
                 src.WriteString("*IDN?", true);
                 string temp = src.ReadString();
                 src.WriteString("OUTPut1:STATe ON", true);*/
                demonstrator.GetSamplesUsingCallback(numSamples, SAMPLE_RATE, AWGFreq);
                // demonstrator.GetSamplesUsingCallback(numSamples, SAMPLE_RATE, AWGFreq);
                return 0;
            }
            else if (line == "n")
            {
                System.Console.WriteLine("Capture cancelled");
                return 2;
            }
            else
            {
                System.Console.WriteLine("Invalid input, please enter 'y' or 'n'. Exiting with code 1");
                return 1;
            }
        }


        /*MAIN TAKES IN COMMAND LINE ARGUMENT OF AWG WAVEFORM FREQUENCY. 
          USES READ AND WRITE FUNCTIONS TO SEND/RECIEVE INFO TO/FROM USER
          MULTIPLE CLASSES AND FUNCTIONS ARE USED. TYPE CASTING CAN BE SEEN 
          IN THE PROCESS SAMPPLES FUNCTION. ARRAY AND LOOP USE CAN BE SEEN IN
          THE SAMPLE COLLECTOR CLASS. ALL REQUIREMENTS SHOULD BE MET FOR C# 
          level 1 MASTERY ACHIEVEMENT*/

        public static int Main(string[] args)
        {
            DemonstrateAsyncPattern demonstrator = new DemonstrateAsyncPattern();
            int AWGFreq = 0;
            bool test = int.TryParse(args[0], out AWGFreq);
            if (args.Length == 0)
            {
                System.Console.WriteLine("Invalid usage");
                demonstrator.PrintError(AWGFreq);
                return 1;
            }
            else if ((!test) || AWGFreq > 500000000 || AWGFreq < 100)
            {
                System.Console.WriteLine("Please use numeric input for AWG frequency between 100Hz and 500MHz.");
                demonstrator.PrintError(AWGFreq);
                return 1;
            }
            else
            {
                demonstrator.calibrateAWG(AWGFreq);
                return 0;
            } 
        }
    }
}
