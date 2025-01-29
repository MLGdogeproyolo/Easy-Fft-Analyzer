using System;
using System.Collections.Generic;
using System.Linq;
using Un4seen.Bass;
using Un4seen.BassWasapi;

namespace VU_Meter
{


    //NOTE:
    //i build these functions (except for getFft, that math is some shadow wizard money gang kind of shit) a pretty meh time ago so i have no fking idea how they work and why they work,
    //the only thing i know is... they work, dont fking touch them.
    //i tried to comment them best i can with the info i still had of maken em, but only god knows the way up from here.

    //Update 2:
    //DO NOT TOUICHJ IT, I MEAN IT./
    // i tried cleaning the code up a bit, rewriting some small functions.
    // short answer, it broke.
    // long er answer, it did not react as wel on the beats as this messy broken piece of.
    // dont optimize it, if you want to make it more readable REWRITE IT!!!
    // dont touch this code for 1 second
    // hands off
    // https://open.spotify.com/track/3VpvErs3oc29iuZLqM1GN9?si=966d90375ceb4829

    internal enum Sensitivity
    {
        Very_Low = 50,
        Low = 25,
        Normal = 0,
        High = -25,
        Very_High = -50
    }

    internal enum RoundingMethod
    {
        Average = 0,
        Maximum = 1
    }

    internal class Analyzer : IDisposable
    {
        #region Developer Configuration

        private float[] _fft = new float[2048]; //buffer for fft data
        private int fftDataLength = (int)BASSData.BASS_DATA_FFT2048;
        private int _lines = 2048 - 1; // number of spectrum lines (-1 becouse i have no idea :/ did it sometime ago)

        internal int deviceFrequency = 44100; //set bitrate of the device 44100 is a "normal" bitrate



        #endregion



        #region System Variables

        private bool _enable;               //enabled status

        private WASAPIPROC _process;        //callback function to obtain data
        private List<byte> _spectrumdata;   //spectrum data buffer
        private bool _initialized;          //initialized flag
        private int devId;                  //device id

        #endregion



        #region Creation/Initialization

        //reference everything so that nothing is null
        public Analyzer()
        {
            //create new process of the WAS(API)
            _process = new WASAPIPROC(Process);
            //initialize the buffer
            _spectrumdata = new List<byte>();
            _initialized = false;

            //add a 0? to the array, dont know, dont ask.
            for (int i = 0; i < _lines; i++)
            {
                ValueOutputList.Add(i, 0);
            }

            Init();
        }


        // WAS(API) callback, required for continuous recording
        private int Process(IntPtr buffer, int length, IntPtr user)
        {
            return length;
        }



        /// <summary>
        /// Get all output devices (may take some time)
        /// </summary>
        /// <returns>A dictionary with the first value being the name of the device and the second value the id of the device</returns>
        public Dictionary<String, int> getDevices()
        {
            Dictionary<String, int> outDev = new Dictionary<String, int>();

            for (int i = 0; i < BassWasapi.BASS_WASAPI_GetDeviceCount(); i++)
            {
                var device = BassWasapi.BASS_WASAPI_GetDeviceInfo(i);
                if (device.IsEnabled && device.IsInput)
                {
                    if(!outDev.TryGetValue(device.name, out int val)) outDev.Add(device.name, i);
                }
            }

            return outDev;
        }

        /// <summary>
        /// Set the active device (only set before enabling)
        /// </summary>
        /// <param name="deviceID">The device ID, Get it from method: <see cref="getDevices"/></param>
        public void SetDevice(int deviceID) { if (!_initialized) devId = deviceID; }

        /// <summary>
        /// Enable the analyzer
        /// </summary>
        public bool Enable
        {
            get { return _enable; }
            set
            {
                _enable = value;
                if (value)
                {
                    if (!_initialized)
                    {

                        bool result = BassWasapi.BASS_WASAPI_Init(devId, 0, 0, BASSWASAPIInit.BASS_WASAPI_BUFFER, 1f, 0.05f, _process, IntPtr.Zero);
                        if (!result)
                        {
                            throw new Exception(Bass.BASS_ErrorGetCode().ToString());
                        }
                        else
                        {
                            _initialized = true;
                        }
                    }
                    BassWasapi.BASS_WASAPI_Start();
                }
                else BassWasapi.BASS_WASAPI_Stop(true);

                //wait some time for the BassWasapi driver to initialize
                System.Threading.Thread.Sleep(250);
            }
        }

        // initialization
        private void Init()
        {
            bool result = false;

            Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_UPDATETHREADS, false);
            result = Bass.BASS_Init(0, deviceFrequency, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);
            if (!result) throw new Exception("Init Error");
        }

        #endregion



        #region Main Calculation/Cleanup

        /// <summary>
        /// Get the volume lever of both channels
        /// </summary>
        /// <param name="leftChannel"></param>
        /// <param name="rightChannel"></param>
        public void getLevel(out int leftChannel, out int rightChannel)
        {
            int valLevel = BassWasapi.BASS_WASAPI_GetLevel();

            Byte[] bytes = BitConverter.GetBytes(valLevel);

            //leftChannel = Map(BitConverter.ToInt16(bytes, 0), 0, 32767, 0, 255);
            //rightChannel = Map(BitConverter.ToInt16(bytes, 2), 0, 32767, 0, 255);


            leftChannel = BitConverter.ToInt16(bytes, 0);
            rightChannel = BitConverter.ToInt16(bytes, 2);


        }

        /// <summary>
        /// Computes fft an gives back a byte list (basically magic going on in this function)
        /// </summary>
        /// <returns></returns>
        public List<byte> getfft()
        {
            if (!_initialized && !_enable) return null;
            _spectrumdata.Clear();

            int ret = BassWasapi.BASS_WASAPI_GetData(_fft, fftDataLength);  //get channel fft data
            
            if (ret < -1) return null;
            int x, y;
            int b0 = 0;

            //computes the spectrum data, the code is taken from a bass_wasapi sample.
            //this math is fking magic, i should've listened at math class ;(
            for (x = 0; x < _lines; x++)
            {
                float peak = 0;
                int b1 = (int)Math.Pow(2, x * 10.0 / (_lines - 1));
                if (b1 > 1023) b1 = 1023;
                if (b1 <= b0) b1 = b0 + 1;
                for (; b0 < b1; b0++)
                {
                    if (peak < _fft[1 + b0]) peak = _fft[1 + b0];
                }
                y = (int)(Math.Sqrt(peak) * 3 * 255 - 4);
                //if (y > 255) y = 255;
                if (y < 0) y = 0;
                _spectrumdata.Add((byte)y);
            }
            return _spectrumdata;


        }


        //cleanup
        public void Dispose()
        {
            BassWasapi.BASS_WASAPI_Free();
            Bass.BASS_Free();
            _initialized = false;
            devId = 0;
        }

        #endregion



        #region Extra Functions

        

        private List<Queue<int>> historyBands = new List<Queue<int>>();
        private int currentHistoryCount = 0;


        /// <summary>
        /// Gets the average data over a span of time for every band (same as _lines)
        /// (Every time you run this function you add the values into a list, see returns why this is important)
        /// </summary>
        /// <param name="data">The fft data <seealso cref="getfft"/></param>
        /// <param name="HistoryCount">The amount of history data to average</param>
        /// <returns>This function returns null if the lists does not have <paramref name="HistoryCount"/>
        /// values in it, if it has it wil return a list with the average of each band</returns>
        public List<int> GetAverage(List<Byte> data, int HistoryCount = 200)
        {

            //if historycount variable changed reset the list and start over
            if (currentHistoryCount != HistoryCount)
            {
                ResetAverage();
                currentHistoryCount = HistoryCount;
            }


            //Check if all queue's are present
            if (historyBands.Count <= _lines)
            {
                //add if this is first time the function has been run
                for (int num = historyBands.Count; num < _lines; num++)
                {
                    historyBands.Add(new Queue<int>());
                }
            }

            //check if there are to much queue's
            if (historyBands.Count > _lines)
            {
                //remove the excess bands
                historyBands.RemoveRange(_lines, historyBands.Count - _lines);
            }




            //add correct data to the corresponding queue
            //in other words, add the fft data to the corresponding line in the queue
            for (int i = 0; i < _lines; i++)
            {

                Queue<int> band = historyBands[i];

               // if (historyBands[i].Count < currentHistoryCount) historyBands[i].Enqueue(data[i]); else { historyBands[i].Dequeue(); historyBands[i].Enqueue(data[i]); }

                //if the queue has not reached the amount of values set by this variable, add it
                if (band.Count < currentHistoryCount)
                {
                    band.Enqueue(data[i]);
                }
                else
                {
                    //else remove 1 value and add another to the end
                    band.Dequeue();

                    band.Enqueue(data[i]);
                }

            }

            //if the count of the first queue is under 200 stop here
            //im going with the information that all queue's have the same amount of values in em, if this is not the case in testing, make a synconization function for it
            if (historyBands[0].Count < currentHistoryCount)
            {
                return null;
            }

            //list for all values
            List<int> outputList = new List<int>();

            //go through each queue
            foreach (Queue<int> queue in historyBands)
            {
                //add average to output list
                outputList.Add((int)queue.Average());
            }

            //return the average of all lines in a list
            return outputList;
        }

        /// <summary>
        /// Resets the List with history data
        /// </summary>
        internal void ResetAverage() { historyBands = new List<Queue<int>>(); }
        
        /// <summary>
        /// Gets the amount of history data in a band
        /// </summary>
        /// <returns>The amount of data</returns>
        internal int GetHistoryCount() { return historyBands[0].Count; }


       


        /// <summary>
        /// Gets the threshold of a band
        /// </summary>
        /// <param name="averageData">The average data</param>
        /// <param name="band">The band to calculate</param>
        /// <param name="threshold">The set threshold, Min = 0, Max = 1</param>
        /// <returns>The calculated threshold for the band</returns>
        internal int GetBandThreshold(List<int> AverageData, int Band, double threshold = 0.75, Sensitivity sensitivity = Sensitivity.Normal) 
        {
            return (int)(AverageData[Band] / threshold) + (int)sensitivity;
        }


        //Format:
        //Band, Value
        Dictionary<int, int> ValueOutputList = new Dictionary<int, int>();

        //Bass region = 0-8 bands
        //mids = 20-85 bands
        //high-mids = 85-165


        /// <summary>
        /// Get the average or maximum value of a number of bands combined, based on a threshold that is calculated using average data
        /// </summary>
        /// <param name="Data">The fft data</param>
        /// <param name="AverageData">The average fft data</param>
        /// <param name="FromBands">Starting band</param>
        /// <param name="ToBands">Ending band</param>
        /// <param name="threshold">Threshold for the bands</param>
        /// <param name="roundingMethod">Rounding method to use</param>
        /// <param name="sensitivity">Sensitivity value</param>
        /// <param name="minimumValue">Minimum band value</param>
        /// <param name="LinkSensitivityAndMinimumValue">If the sensitivity parameter should affect minimumValue</param>
        /// <param name="DropValuesBy">If the value is lower than the previous one drop by ...</param>
        /// <returns>The average value</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        internal int GetTonesValueBasedOnAverage(List<Byte> Data, List<int> AverageData, int FromBands, int ToBands, double threshold = 0.75, RoundingMethod roundingMethod = RoundingMethod.Average, Sensitivity sensitivity = Sensitivity.Normal, int minimumValue = 25, Boolean LinkSensitivityAndMinimumValue = true, int DropValuesBy = 20)
        {

            //Check if all arguments are within spec
            //if not throw exception
            if (FromBands >= ToBands) throw new ArgumentOutOfRangeException("frombands", "FromBands Cannot be bigger or equal than ToBands");
            if (ToBands > _lines) throw new ArgumentOutOfRangeException("tobands", "ToBands Cannot be bigger than the total amount of spectrum lines");

            if (Data.Count < _lines) throw new ArgumentNullException("data", "data Cannot be empty");
            if (AverageData.Count < _lines) throw new ArgumentNullException("averagedata", "AverageData Cannot be empty");

            if (DropValuesBy > 255) throw new ArgumentOutOfRangeException("dropvaluesby", "DropValuesBy Cannot be bigger than 255");
            if (DropValuesBy < 0) throw new ArgumentOutOfRangeException("dropvaluesby", "DropValuesBy Cannot be smaller than 0");

            //With exception of the threshold argument for no reason :/
            //(more like becouse it is user changable is does not need explicit handling just make sure it is within spec)
            if (threshold > 1) threshold = 1;
            if (threshold < 0) threshold = 0;

            //Add/Remove the sensitivity argument from the minimum value
            //The sensitivity is a sort of value added/removed from minimumValue, its more like a quick setting to change it.
            //if you want more control over the values in the sensitivity, keep this false and add/remove from minimum value
            if (LinkSensitivityAndMinimumValue) minimumValue = minimumValue + (int)sensitivity;


            //Drop all existing bands with the given argument (DropValuesBy)
            for(int i = 0; i < _lines; i++)
            {
                //get the value from the list
                int val = ValueOutputList[i];
                //if it is smaller than what needs to be removed, change it to 0
                if (val <= DropValuesBy) ValueOutputList[i] = 0;
                //if not remove it
                else ValueOutputList[i] = val - DropValuesBy;
            }

            //New list for data
            List<int> OutList = new List<int>();

            //Iterate through each band
            for (int i = FromBands; i < ToBands+1; i++)
            {
                //Get the threshold for the band
                int BandThreshold = GetBandThreshold(AverageData, i, threshold, sensitivity);
                //Get the value of the band
                int BandVal = Data[i];

                int val = ValueOutputList[i];

                //Check if the band is greater than the minimum value and threshold
                if (BandVal > minimumValue && BandVal > BandThreshold)
                {
                    //Check if minimum value is bigger than BandThreshold otherwise use Bandthreshold
                    int toRem = minimumValue > BandThreshold ? minimumValue : BandThreshold;

                    //Get the max
                    int max = 255 - toRem;
                    //Shift band value max back to 255 - toRem
                    int num = BandVal - toRem;

                    //Map value back to 0 to 255
                    num = Map(num, 0, max, 0, 255);

                    //if dropvalues by is 0 than just add the value
                    if (DropValuesBy == 0) OutList.Add(num);
                    else
                    //check if the value is bigger than in the list
                    if (num > val)
                    {
                        //change to new value
                        ValueOutputList[i] = num;
                        //And change the value itself
                        val = num;
                    }
                }
                //add the value to the output list
                if (DropValuesBy != 0 && val > 0)
                {
                    OutList.Add(val);
                }
            }
            //if none was added add a zero to avoid a null exception
            if (OutList.Count == 0) OutList.Add(0);

            //Switch on rounding method
            switch (roundingMethod)
            {
                case RoundingMethod.Average: return (int)OutList.Average();
                case RoundingMethod.Maximum: return (int)OutList.Max();
            }

            //If for one reason or another the user or programmer decides to do a funny and use another rounding method other than the ones given than fok right of with a -1
            return -1;

        }


        /// <summary>
        /// Get the average or maximum value of a number of bands combined
        /// </summary>
        /// <param name="Data">The fft data</param> 
        /// <param name="FromBands">Starting band</param>
        /// <param name="ToBands">Ending band</param>
        /// <param name="roundingMethod">Rounding method to use</param>
        /// <param name="sensitivity">Sensitivity value (useless if the <paramref name="LinkSensitivityAndMinimumValue"/> is set to false)</param>
        /// <param name="minimumValue">Minimum band value</param>
        /// <param name="LinkSensitivityAndMinimumValue">If the sensitivity parameter should affect minimumValue</param>
        /// <returns>The average value</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        internal int GetTonesValue(List<Byte> Data, int FromBands, int ToBands, RoundingMethod roundingMethod = RoundingMethod.Average, Sensitivity sensitivity = Sensitivity.Normal, int minimumValue = 25, Boolean LinkSensitivityAndMinimumValue = true, int DropValuesBy = 20)
        {
            if (FromBands >= ToBands) throw new ArgumentOutOfRangeException("frombands", "FromBands Cannot be bigger or equal than ToBands");
            if (ToBands > _lines) throw new ArgumentOutOfRangeException("tobands", "ToBands Cannot be bigger than the total amount of spectrum lines");
            if (Data.Count < _lines) throw new ArgumentNullException("data", "data Cannot be empty");

            if (DropValuesBy > 255) throw new ArgumentOutOfRangeException("dropvaluesby", "DropValuesBy Cannot be bigger than 255");
            if (DropValuesBy < 0) throw new ArgumentOutOfRangeException("dropvaluesby", "DropValuesBy Cannot be smaller than 0");

            if (LinkSensitivityAndMinimumValue) minimumValue = minimumValue + (int)sensitivity;

            //Drop all existing bands with the given argument (DropValuesBy)
            for (int i = 0; i < _lines; i++)
            {
                int val = ValueOutputList[i];
                if (val <= DropValuesBy) ValueOutputList[i] = 0;
                else ValueOutputList[i] = val - DropValuesBy;
            }

            List<int> OutList = new List<int>();

            for (int i = FromBands; i < ToBands + 1; i++)
            {
                int BandVal = Data[i];

                int val = ValueOutputList[i];

                if (BandVal > minimumValue)
                {
                    int num = Map(BandVal, minimumValue, 255, 0, 255);

                    //if dropvalues by is 0 than just add the value
                    if (DropValuesBy == 0) OutList.Add(num);
                    else
                    //check if the value is bigger than in the list
                    if (num > val)
                    {
                        //change to new value
                        ValueOutputList[i] = num;
                        //And change the value itself
                        val = num;
                    }


                }
                //add the value to the output list
                if (DropValuesBy != 0 && val > 0)
                {
                    OutList.Add(val);
                }
            }

            if (OutList.Count == 0) OutList.Add(0);

            switch (roundingMethod)
            {
                case RoundingMethod.Average: return (int)OutList.Average();
                case RoundingMethod.Maximum: return (int)OutList.Max();
            }


            return -1;

        }

        public int Map(int value, int fromLow, int fromHigh, int toLow, int toHigh)
        {
            try
            {
                return (value - fromLow) * (toHigh - toLow) / (fromHigh - fromLow) + toLow;
            }catch(DivideByZeroException e)
            {
                return -1;
            }
        }

        #endregion


    }
}

