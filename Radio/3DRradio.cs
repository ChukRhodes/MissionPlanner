﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.IO;
using MissionPlanner.Controls.BackstageView;
using MissionPlanner.Arduino;
using MissionPlanner.Comms;
using log4net;
using System.Reflection;
using System.Text.RegularExpressions;

namespace MissionPlanner
{
    public partial class _3DRradio : UserControl
    {
        public delegate void LogEventHandler(string message, int level = 0);

        public delegate void ProgressEventHandler(double completed);

        string firmwarefile = Path.GetDirectoryName(Application.ExecutablePath) + Path.DirectorySeparatorChar + "radio.hex";

        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        enum mavlink_option : int
        {
            RawData = 0,
            Mavlink = 1,
            LowLatency = 2,
        }

        /*
ATI5
S0: FORMAT=25
S1: SERIAL_SPEED=57
S2: AIR_SPEED=64
S3: NETID=40
S4: TXPOWER=30
S5: ECC=1
S6: MAVLINK=1
S7: OPPRESEND=1
S8: MIN_FREQ=915000
S9: MAX_FREQ=928000
S10: NUM_CHANNELS=50
S11: DUTY_CYCLE=100
S12: LBT_RSSI=0
S13: MANCHESTER=0
S14: RTSCTS=0
S15: MAX_WINDOW=131

        MultiPoint Firmware
S0: FORMAT=27
S1: SERIAL_SPEED=57
S2: AIR_SPEED=64
S3: NETID=40
S4: TXPOWER=30
S5: ECC=0                   // Seems disabled in FW for all hardware
S6: MAVLINK=1
S7: OPPRESEND=1
S8: MIN_FREQ=915000
S9: MAX_FREQ=928000
S10: NUM_CHANNELS=50
S11: DUTY_CYCLE=100
S12: LBT_RSSI=0
S13: MANCHESTER=0
S14: RTSCTS=0
S15: NODEID=1
S16: NODEDESTINATION=65535
S17: SYNCANY=0
S18: NODECOUNT=4
         */

        public _3DRradio()
        {
            InitializeComponent();

            // hide advanced view
            //SPLIT_local.Panel2Collapsed = true;
            //SPLIT_remote.Panel2Collapsed = true;

            // setup netid
            S3.DataSource = Enumerable.Range(0, 500).ToArray();
            RS3.DataSource = Enumerable.Range(0, 500).ToArray();

            var dict = Enum.GetValues(typeof(mavlink_option))
               .Cast<mavlink_option>()
               .ToDictionary(t => (int)t, t => t.ToString());

            S6.DisplayMember = "Value";
            S6.ValueMember = "Key";
            S6.DataSource = dict.ToArray();
            RS6.DisplayMember = "Value";
            RS6.ValueMember = "Key";
            RS6.DataSource = dict.ToArray();

            // S15 changes by FORMAT
            S15.DataSource = Enumerable.Range(33, 131 - 32).ToArray();
            RS15.DataSource = Enumerable.Range(33, 131 - 32).ToArray();
        }

        bool getFirmware(uploader.Uploader.Board device, bool custom = false)
        {
            if (custom)
            {
                return getFirmwareLocal(device);
            }

            if (device == uploader.Uploader.Board.DEVICE_ID_HM_TRP)
            {
                if (beta)
                { 
                    return Common.getFilefromNet("http://firmware.diydrones.com/SiK/beta/radio~hm_trp.ihx", firmwarefile); 
                }
                else
                {
                    return Common.getFilefromNet("http://firmware.diydrones.com/SiK/stable/radio~hm_trp.ihx", firmwarefile);
                }
            }
            else if (device == uploader.Uploader.Board.DEVICE_ID_RFD900)
            {
                if (beta)
                {
                    return Common.getFilefromNet("http://firmware.diydrones.com/SiK/beta/radio~rfd900.ihx", firmwarefile);
                }
                else
                {
                    return Common.getFilefromNet("http://rfdesign.com.au/firmware/radio.rfd900.hex", firmwarefile);
                }
            }
            else if (device == uploader.Uploader.Board.DEVICE_ID_RFD900A)
            {
                if (beta)
                {
                    return Common.getFilefromNet("http://firmware.diydrones.com/SiK/beta/radio~rfd900a.ihx", firmwarefile);
                }
                else
                {
                    return Common.getFilefromNet("http://rfdesign.com.au/firmware/radio.rfd900a.hex", firmwarefile);
                }

            }
            else if (device == uploader.Uploader.Board.DEVICE_ID_RFD900U)
            {
                if (beta)
                {
                    return Common.getFilefromNet("http://rfdesign.com.au/firmware/radio~rfd900u.ihx", firmwarefile);
                }
                else
                {
                    return Common.getFilefromNet("http://rfdesign.com.au/firmware/radio~rfd900u.ihx", firmwarefile);
                }

            }
            else if (device == uploader.Uploader.Board.DEVICE_ID_RFD900P)
            {
                if (beta)
                {
                    return Common.getFilefromNet("http://rfdesign.com.au/firmware/radio~rfd900p.ihx", firmwarefile);
                }
                else
                {
                    return Common.getFilefromNet("http://rfdesign.com.au/firmware/radio~rfd900p.ihx", firmwarefile);
                }

            }
            else
            {
                return false;
            }
        }

        bool beta = false;

        bool getFirmwareLocal(uploader.Uploader.Board device)
        {
            using (OpenFileDialog openFileDialog1 = new OpenFileDialog())
            {

                openFileDialog1.Filter = "Firmware|*.hex;*.ihx";
                openFileDialog1.RestoreDirectory = true;
                openFileDialog1.Multiselect = false;

                if (openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        File.Copy(openFileDialog1.FileName, firmwarefile, true);
                    }
                    catch (Exception ex)
                    {
                        CustomMessageBox.Show("Error copying file\n" + ex.ToString(), "ERROR");
                        return false;
                    }
                    return true;
                }

                return false;
            }
        }

        void Sleep(int mstimeout, ICommsSerial comPort = null)
        {
            DateTime endtime = DateTime.Now.AddMilliseconds(mstimeout);

            while (DateTime.Now < endtime)
            {
                System.Threading.Thread.Sleep(1);
                Application.DoEvents();

                // prime the mavlinkserial loop with data.
                if (comPort != null) { 
                    int test = comPort.BytesToRead;
                    test++;
                }
            }
        }

        private void BUT_upload_Click(object sender, EventArgs e)
        {
            UploadFW(false);
        }

        private void UploadFW(bool custom = false)
        {
            ICommsSerial comPort = new SerialPort();

            uploader.Uploader uploader = new uploader.Uploader();

            if (MainV2.comPort.BaseStream.IsOpen)
            {
                try
                {
                    comPort = new MAVLinkSerialPort(MainV2.comPort, 0);//MAVLink.SERIAL_CONTROL_DEV.TELEM1);

                    uploader.PROG_MULTI_MAX = 64;

                }
                catch (Exception ex) 
                {
                    CustomMessageBox.Show("Error " + ex.ToString());
                }
            }

            try
            {
                comPort.PortName = MainV2.comPort.BaseStream.PortName;
                comPort.BaudRate = 115200;

                comPort.Open();

            }
            catch { CustomMessageBox.Show("Invalid ComPort or in use"); return; }

            // prep what we are going to upload
            uploader.IHex iHex = new uploader.IHex();

            iHex.LogEvent += new LogEventHandler(iHex_LogEvent);

            iHex.ProgressEvent += new ProgressEventHandler(iHex_ProgressEvent);

            bool bootloadermode = false;

            // attempt bootloader mode
            try
            {
                uploader_ProgressEvent(0);
                uploader_LogEvent("Trying Bootloader Mode");
                uploader.port = comPort;
                uploader.connect_and_sync();

                uploader.ProgressEvent += new ProgressEventHandler(uploader_ProgressEvent);
                uploader.LogEvent += new LogEventHandler(uploader_LogEvent);

                uploader_LogEvent("In Bootloader Mode");
                bootloadermode = true;
            }
            catch
            {
                // cleanup bootloader mode fail, and try firmware mode
                comPort.Close();
                if (MainV2.comPort.BaseStream.IsOpen)
                {
                    // default baud... guess
                    comPort.BaudRate = 57600;
                } else {
                    comPort.BaudRate = MainV2.comPort.BaseStream.BaudRate;
                }
                try
                {
                    comPort.Open();
                }
                catch { CustomMessageBox.Show("Error opening port", "Error"); return; }

                uploader.ProgressEvent += new ProgressEventHandler(uploader_ProgressEvent);
                uploader.LogEvent += new LogEventHandler(uploader_LogEvent);

                uploader_LogEvent("Trying Firmware Mode");
                bootloadermode = false;
            }

            // check for either already bootloadermode, or if we can do a ATI to ID the firmware 
            if (bootloadermode || doConnect(comPort))
            {
                // put into bootloader mode/udpate mode
                if (!bootloadermode)
                {
                    try
                    {
                        comPort.Write("AT&UPDATE\r\n");
                        string left = comPort.ReadExisting();
                        log.Info(left);
                        Sleep(700);
                        comPort.BaudRate = 115200;
                    }
                    catch { }
                }

                // force sync after changing baudrate
                uploader.connect_and_sync();

                global::uploader.Uploader.Board device = global::uploader.Uploader.Board.FAILED;
                global::uploader.Uploader.Frequency freq = global::uploader.Uploader.Frequency.FAILED;

                // get the device type and frequency in the bootloader
                uploader.getDevice(ref device, ref freq);

                // get firmware for this device
                if (getFirmware(device, custom))
                {
                    // load the hex
                    try
                    {
                        iHex.load(firmwarefile);
                    }
                    catch { CustomMessageBox.Show("Bad Firmware File"); goto exit; }

                    // upload the hex and verify
                    try
                    {
                        uploader.upload(comPort, iHex);
                    }
                    catch (Exception ex) { CustomMessageBox.Show("Upload Failed " + ex.Message); }

                }
                else
                {
                    CustomMessageBox.Show("Failed to download new firmware");
                }

            }
            else
            {
                CustomMessageBox.Show("Failed to identify Radio");
            }

        exit:
            if (comPort.IsOpen)
                comPort.Close();
        }

        void iHex_ProgressEvent(double completed)
        {
            try
            {
                Progressbar.Value = (int)(completed * 100);
                Application.DoEvents();
            }
            catch { }
        }

        void uploader_LogEvent(string message, int level = 0)
        {
            try
            {
                if (level == 0)
                {
                    Console.Write(message);
                    lbl_status.Text = message;
                    log.Info(message);
                    Application.DoEvents();
                }
                else if (level < 5) // 5 = byte data
                {
                    log.Debug(message);
                }
            }
            catch { }
        }

        void iHex_LogEvent(string message, int level = 0)
        {
            try
            {
                if (level == 0)
                {
                    lbl_status.Text = message;
                    Console.WriteLine(message);
                    log.Info(message);
                    Application.DoEvents();
                }
            }
            catch { }
        }

        /// <summary>
        /// Log to various places
        /// </summary>
        /// <param name="message">Text to display</param>
        /// <param name="level">0 = log.Info only, 1 = UI only, 2 = UI and log.Info, 3 = UI / log.Info / Console</param>
        void comLog(string message, int level = 0)
        {
            try
            {
                if (level == 0)
                {
                    log.Info(message);
                }
                if (level == 1)
                {
                    lbl_status.Text = message;
                    Application.DoEvents();
                }
                if (level == 2)
                {
                    lbl_status.Text = message;
                    log.Info(message);
                    Application.DoEvents();
                }
                if (level == 3)
                {
                    Console.Write(message);
                    lbl_status.Text = message;
                    log.Info(message);
                    Application.DoEvents();
                }
            }
            catch { }
        }

        void uploader_ProgressEvent(double completed)
        {
            try
            {
                Progressbar.Value = (int)(completed * 100);
                Application.DoEvents();
            }
            catch { }
        }

        private void BUT_savesettings_Click(object sender, EventArgs e)
        {
            ICommsSerial comPort = new SerialPort();

            try
            {
                if (MainV2.comPort.BaseStream.IsOpen)
                {
                    comPort = new MAVLinkSerialPort(MainV2.comPort, 0);//MAVLink.SERIAL_CONTROL_DEV.TELEM1);

                    comPort.BaudRate = 57600;
                }
                else
                {
                    comPort.PortName = MainV2.comPort.BaseStream.PortName;
                    comPort.BaudRate = MainV2.comPort.BaseStream.BaudRate;
                }

                comPort.ReadTimeout = 4000;

                comPort.Open();


            }
            catch { CustomMessageBox.Show("Invalid ComPort or in use"); return; }

            comLog("Connecting", 1);

            if (doConnect(comPort))
            {
                // cleanup
                doCommand(comPort, "AT&T");

                comPort.DiscardInBuffer();

                if (RTI.Text != "")
                {
                    // remote
                    string answer = doCommand(comPort, "RTI5", true);

                    string[] items = answer.Split(new char[] {'\n'},StringSplitOptions.RemoveEmptyEntries);

                    foreach (string item in items)
                    {
                        if (item.StartsWith("S"))
                        {
                            string[] values = item.Split(':', '=');

                            if (values.Length == 3)
                            {
                                Control[] controls = this.Controls.Find("R" + values[0].Trim(), true);

                                if (controls.Length > 0)
                                {
                                    if (controls[0].GetType() == typeof(CheckBox))
                                    {
                                        string value = ((CheckBox)controls[0]).Checked ? "1" : "0";

                                        if (value != values[2].Trim())
                                        {
                                            string cmdanswer = doCommand(comPort, "RT" + values[0].Trim() + "=" + value + "\r");

                                            if (cmdanswer.Contains("OK"))
                                            {

                                            }
                                            else
                                            {
                                                CustomMessageBox.Show("Set Command error");
                                            }
                                        }
                                    }
                                    else if (controls[0] is TextBox)
                                    {

                                    }
                                    else if (controls[0].Name.Contains("S6")) //
                                    {
                                        if (((ComboBox)controls[0]).SelectedValue.ToString() != values[2].Trim())
                                        {
                                            string cmdanswer = doCommand(comPort, "RT" + values[0].Trim() + "=" + ((ComboBox)controls[0]).SelectedValue + "\r");

                                            if (cmdanswer.Contains("OK"))
                                            {

                                            }
                                            else
                                            {
                                                CustomMessageBox.Show("Set Command error");
                                            }
                                        }
                                    }
                                    else if (controls[0] is ComboBox)
                                    {
                                        if (controls[0].Text != values[2].Trim()) {
                                        string cmdanswer = doCommand(comPort, "RT" + values[0].Trim() + "=" + controls[0].Text + "\r");

                                        if (cmdanswer.Contains("OK"))
                                        {

                                        }
                                        else
                                        {
                                            CustomMessageBox.Show("Set Command error");
                                        }
                                    }
                                    }
                                }
                            }
                            else
                            {
                                // bad ?ti5 line
                            }
                        }
                    }

                    // write it
                    doCommand(comPort, "RT&W");

                    // return to normal mode
                    doCommand(comPort, "RTZ");

                    Sleep(100);
                }

                comPort.DiscardInBuffer();
                {
                    //local
                    string answer = doCommand(comPort, "ATI5", true);

                    string[] items = answer.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (string item in items)
                    {
                        if (item.StartsWith("S"))
                        {
                            string[] values = item.Split(':', '=');

                            if (values.Length == 3)
                            {
                                Control[] controls = this.Controls.Find(values[0].Trim(), true);

                                if (controls.Length > 0)
                                {
                                    if (controls[0].GetType() == typeof(CheckBox))
                                    {
                                        string value = ((CheckBox)controls[0]).Checked ? "1" : "0";

                                        if (value != values[2].Trim())
                                        {
                                            string cmdanswer = doCommand(comPort, "AT" + values[0].Trim() + "=" + value + "\r");

                                            if (cmdanswer.Contains("OK"))
                                            {

                                            }
                                            else
                                            {
                                                CustomMessageBox.Show("Set Command error");
                                            }
                                        }
                                    }
                                    else if (controls[0] is TextBox)
                                    {

                                    }
                                    else if (controls[0].Name.Contains("S6")) //
                                    {
                                        if (((ComboBox)controls[0]).SelectedValue.ToString() != values[2].Trim())
                                        {
                                            string cmdanswer = doCommand(comPort, "AT" + values[0].Trim() + "=" + ((ComboBox)controls[0]).SelectedValue + "\r");

                                            if (cmdanswer.Contains("OK"))
                                            {

                                            }
                                            else
                                            {
                                                CustomMessageBox.Show("Set Command error");
                                            }
                                        }
                                    }
                                    else if (controls[0] is ComboBox)
                                    {
                                        if (controls[0].Text != values[2].Trim())
                                        {
                                            string cmdanswer = doCommand(comPort, "AT" + values[0].Trim() + "=" + controls[0].Text + "\r");

                                            if (cmdanswer.Contains("OK"))
                                            {

                                            }
                                            else
                                            {
                                                CustomMessageBox.Show("Set Command error");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // write it
                    doCommand(comPort, "AT&W");


                    // return to normal mode
                    doCommand(comPort, "ATZ");
                }

                lbl_status.Text = "Done";
            }
            else
            {

                // return to normal mode
                doCommand(comPort, "ATZ");

                lbl_status.Text = "Fail";
                CustomMessageBox.Show("Failed to enter command mode");
            }


            comPort.Close();
        }

        public static IEnumerable<int> Range(int start, int step, int end)
        {
            List<int> list = new List<int>();

            for (int a = start; a <= end; a += step)
            {
                list.Add(a);
            }

            return list;
        }


        private void BUT_getcurrent_Click(object sender, EventArgs e)
        {
            ICommsSerial comPort = new SerialPort();



            try
            {
                if (MainV2.comPort.BaseStream.IsOpen)
                {
                    comPort = new MAVLinkSerialPort(MainV2.comPort, 0);//MAVLink.SERIAL_CONTROL_DEV.TELEM1);

                    comPort.BaudRate = 57600;
                }
                else
                {
                    comPort.PortName = MainV2.comPort.BaseStream.PortName;
                    comPort.BaudRate = MainV2.comPort.BaseStream.BaudRate;
                }

                comPort.ReadTimeout = 4000;

                comPort.Open();


            }
            catch { CustomMessageBox.Show("Invalid ComPort or in use"); return; }

            try
            {
                if (doConnect(comPort))
                {
                    // cleanup
                    doCommand(comPort, "AT&T");

                    comPort.DiscardInBuffer();

                    comLog("Doing Command ATI & RTI", 2);

                    ATI.Text = doCommand(comPort, "ATI");

                    RTI.Text = doCommand(comPort, "RTI");

                    uploader.Uploader.Frequency freq = (uploader.Uploader.Frequency)Enum.Parse(typeof(uploader.Uploader.Frequency), doCommand(comPort, "ATI3"));
                    uploader.Uploader.Board board = (uploader.Uploader.Board)Enum.Parse(typeof(uploader.Uploader.Board), doCommand(comPort, "ATI2"));

                    ATI3.Text = freq.ToString();

                    ATI2.Text = board.ToString();
                    try
                    {
                        string resp = doCommand(comPort, "RTI2");
                        if (resp.Trim() != "")
                            RTI2.Text = ((uploader.Uploader.Board)Enum.Parse(typeof(uploader.Uploader.Board), resp)).ToString();
                    }
                    catch { }
                    // 8 and 9
                    if (freq == uploader.Uploader.Frequency.FREQ_915)
                    {
                        S8.DataSource = Range(895000, 1000, 935000);
                        RS8.DataSource = Range(895000, 1000, 935000);

                        S9.DataSource = Range(895000, 1000, 935000);
                        RS9.DataSource = Range(895000, 1000, 935000);
                    }
                    else if (freq == uploader.Uploader.Frequency.FREQ_433)
                    {
                        S8.DataSource = Range(414000, 50, 460000);
                        RS8.DataSource = Range(414000, 50, 460000);

                        S9.DataSource = Range(414000, 50, 460000);
                        RS9.DataSource = Range(414000, 50, 460000);
                    }
                    else if (freq == uploader.Uploader.Frequency.FREQ_868)
                    {
                        S8.DataSource = Range(849000, 1000, 889000);
                        RS8.DataSource = Range(849000, 1000, 889000);

                        S9.DataSource = Range(849000, 1000, 889000);
                        RS9.DataSource = Range(849000, 1000, 889000);
                    }

                    if (board == uploader.Uploader.Board.DEVICE_ID_RFD900 || board == uploader.Uploader.Board.DEVICE_ID_RFD900A 
                        || board == uploader.Uploader.Board.DEVICE_ID_RFD900P || board == uploader.Uploader.Board.DEVICE_ID_RFD900U)
                    {
                        S4.DataSource = Range(1, 1, 30);
                        RS4.DataSource = Range(1, 1, 30);
                    }


                    LRSSI.Text = doCommand(comPort, "ATI7", true).Trim();

                    comLog("Doing Command ATI5", 2);

                    string answer = doCommand(comPort, "ATI5", true);

                    string[] items = answer.Split('\n');

                    foreach (string item in items)
                    {
                        if (item.StartsWith("S"))
                        {
                            string[] values = item.Split(':', '=');

                            if (values.Length == 3)
                            {
                                Control[] controls = this.Controls.Find(values[0].Trim(), true);

                                if (controls.Length > 0)
                                {
                                    controls[0].Enabled = true;

                                    if (controls[0] is CheckBox)
                                    {
                                        ((CheckBox)controls[0]).Checked = values[2].Trim() == "1";
                                    }
                                    else if (controls[0] is TextBox)
                                    {
                                        ((TextBox)controls[0]).Text = values[2].Trim();
                                    }
                                    else if (controls[0].Name.Contains("S6")) //
                                    {
                                        var ans = Enum.Parse(typeof(mavlink_option), values[2].Trim());
                                        ((ComboBox)controls[0]).Text = ans.ToString();
                                    }
                                    else if (controls[0] is ComboBox)
                                    {
                                        ((ComboBox)controls[0]).Text = values[2].Trim();
                                    }
                                }
                            }
                        }
                    }

                    // remote
                    foreach (Control ctl in groupBox2.Controls)
                    {
                        if (ctl.Name.StartsWith("RS") && ctl.Name != "RSSI")
                            ctl.ResetText();
                    }

                    comPort.DiscardInBuffer();

                    comLog("Doing Command RTI5", 2);

                    answer = doCommand(comPort, "RTI5", true);

                    items = answer.Split('\n');

                    foreach (string item in items)
                    {
                        if (item.StartsWith("S"))
                        {
                            string[] values = item.Split(':', '=');

                            if (values.Length == 3)
                            {
                                Control[] controls = this.Controls.Find("R" + values[0].Trim(), true);

                                if (controls.Length == 0)
                                    continue;

                                controls[0].Enabled = true;

                                if (controls[0] is CheckBox)
                                {
                                    ((CheckBox)controls[0]).Checked = values[2].Trim() == "1";
                                }
                                else if (controls[0] is TextBox)
                                {
                                    ((TextBox)controls[0]).Text = values[2].Trim();
                                }
                                else if (controls[0].Name.Contains("S6")) //
                                {
                                    var ans = Enum.Parse(typeof(mavlink_option), values[2].Trim());
                                    ((ComboBox)controls[0]).Text = ans.ToString();
                                }
                                else if (controls[0] is ComboBox)
                                {
                                    ((ComboBox)controls[0]).Text = values[2].Trim();
                                }
                            }
                            else
                            {
                                /*
                                if (item.Contains("S15"))
                                {
                                    answer = doCommand(comPort, "RTS15?");
                                    int rts15 = 0;
                                    if (int.TryParse(answer, out rts15))
                                    {
                                        RS15.Enabled = true;
                                        RS15.Text = rts15.ToString();
                                    }
                                }
                                */
                                log.Info("Odd config line :" + item);
                            }
                        }
                    }

                    // off hook
                    doCommand(comPort, "ATO");

                    comLog("Done", 2);
                }
                else
                {

                    // off hook
                    doCommand(comPort, "ATO");

                    comLog("Fail", 2);
                    CustomMessageBox.Show("Failed to enter command mode");
                }

                comPort.Close();

                BUT_Syncoptions.Enabled = true;

                BUT_savesettings.Enabled = true;
            }
            catch (Exception ex)
            {
                try
                {
                    if (comPort != null)
                        comPort.Close();
                }
                catch { }
                comLog("Error", 2);
                CustomMessageBox.Show("Error during read " + ex.ToString());
                return;
            }
        }

        string Serial_ReadLine(ICommsSerial comPort)
        {
            StringBuilder sb = new StringBuilder();
            DateTime Deadline = DateTime.Now.AddMilliseconds(comPort.ReadTimeout);

            while (DateTime.Now < Deadline)
            {
                if (comPort.BytesToRead > 0)
                {
                    byte data = (byte)comPort.ReadByte();
                    sb.Append((char)data);
                    if (data == '\n')
                        break;
                }
            }

            return sb.ToString();
        }

        public string trimResponse(string line)
        {
            // Find nodeId (for later use)
            Regex nodeMatch = new Regex(@"\[(\d)\]\s*");
            string nodeId = nodeMatch.Match(line).Groups[1].ToString();

            // Trim prefix
            Regex trimMatch = new Regex(@"(\[\d\]\s*)");
            Match trimed = trimMatch.Match(line);
            if (trimed.Success)
                line = line.Replace(trimed.Groups[1].ToString(), "");

            char[] ends = { '\r', '\n' };
            comLog("TRIM " + trimed.Success + "   " + "nodeId='" + nodeId + "' trimed='" + trimed + "' Response = '" + line.TrimEnd(ends) + "'");
            
            // line should now be the same as expected even without multi point prefix

            return line;
        }

        public string doCommand(ICommsSerial comPort, string cmd, bool multiLineResponce = false, int level = 0)
        {
            if (!comPort.IsOpen)
                return "";

            comPort.DiscardInBuffer();

            comLog("Doing Command " + cmd, 1);
            
            comPort.Write(cmd + "\r\n");

            comPort.ReadTimeout = 1000;

            // command echo
            var cmdecho = Serial_ReadLine(comPort);

            if (cmdecho.Contains(cmd))
            {
                var value = "";

                if (multiLineResponce)
                {
                    DateTime deadline = DateTime.Now.AddMilliseconds(1000);
                    while (comPort.BytesToRead > 0 || DateTime.Now < deadline)
                    {
                        try
                        {
                            value = value + trimResponse(Serial_ReadLine(comPort));
                        }
                        catch { value = value + trimResponse(comPort.ReadExisting()); }
                    }
                }
                else
                {
                    value = trimResponse(Serial_ReadLine(comPort));

                    if (value == "" && level == 0)
                    {
                        return trimResponse(doCommand(comPort, cmd, multiLineResponce, 1));
                    }
                }
                char[] ends = { '\r', '\n' };
                comLog(value.TrimEnd(ends), 1);
                History.AppendText(cmd.Trim() + " " + value.TrimEnd(ends) + Environment.NewLine);

                return value;
            }

            comPort.DiscardInBuffer();

            // try again
            if (level == 0)
                return trimResponse(doCommand(comPort, cmd, multiLineResponce, 1));

            return "";
        }

        public bool doConnect(ICommsSerial comPort)
        {
            char[] ends = { '\r', '\n' };

            try
            {
                comLog("Connecting", 2);

                int trys = 1;

                // setup a known enviroment
                comPort.Write("ATO\r\n");

                retry:

                // wait
                Sleep(1500, comPort);
                comPort.DiscardInBuffer();
                // send config string
                comPort.Write("+++");
                Sleep(1500,comPort);
                // check for config response "OK"
                comLog("Connect btr " + comPort.BytesToRead + " baud " + comPort.BaudRate, 2);
                // allow time for data/response
                
                if (comPort.BytesToRead == 0 && trys <= 3) {
                    trys++;
                    comLog("doConnect retry", 2);
                        goto retry;
                }

                byte[] buffer = new byte[20];
                int len = comPort.Read(buffer, 0, buffer.Length);
                string conn = ASCIIEncoding.ASCII.GetString(buffer, 0, len);
                comLog("Connect first response " + conn.TrimEnd(ends) + " " + conn.Length);

                if (conn.Contains("OK"))
                {
                    //return true;
                }
                else
                {
                    // cleanup incase we are already in cmd mode
                    comPort.Write("\r\n");
                }

                doCommand(comPort, "AT&T");

                string version = doCommand(comPort, "ATI");

                Regex regex = new Regex(@"SiK\s+(.*)\s+on\s+(.*)");

                if (regex.IsMatch(version))
                {
                    comLog("Connected: " + version.TrimEnd(ends), 2);
                    return true;
                }

                comLog("Connect Version: " + version.TrimEnd(ends));
                return false;
            }
            catch { return false; }
        }

        private void BUT_Syncoptions_Click(object sender, EventArgs e)
        {
            RS2.Text = S2.Text;
            RS3.Text = S3.Text;
            RS5.Checked = S5.Checked;
            RS6.Text = S6.Text;
            RS8.Text = S8.Text;
            RS9.Text = S9.Text;
            RS10.Text = S10.Text;
            RS15.Text = S15.Text;
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            CustomMessageBox.Show(@"The 3DR Radios have 2 status LEDs, one red and one green.
green LED blinking - searching for another radio 
green LED solid - link is established with another radio 
red LED flashing - transmitting data 
red LED solid - in firmware update mode");
        }

        private void BUT_resettodefault_Click(object sender, EventArgs e)
        {
            ICommsSerial comPort = new SerialPort();

            try
            {
                comPort.PortName = MainV2.comPort.BaseStream.PortName;
                comPort.BaudRate = MainV2.comPort.BaseStream.BaudRate;

                comPort.ReadTimeout = 4000;

                comPort.Open();


            }
            catch { CustomMessageBox.Show("Invalid ComPort or in use"); return; }

            comLog("Connecting", 2);

            if (doConnect(comPort))
            {
                // cleanup
                doCommand(comPort, "AT&T"); // Disbale test modes

                comPort.DiscardInBuffer();

                comLog("Doing Command ATI & AT&F", 1);

                doCommand(comPort, "AT&F"); // Load default params

                doCommand(comPort, "AT&W"); // Save params

                comLog("Reset", 2);

                doCommand(comPort, "ATZ");

                comPort.Close();

                BUT_getcurrent_Click(sender, e);
            }
            else
            {

                // off hook
                doCommand(comPort, "ATO");

                comLog("Fail", 2);
                History.AppendText("Failed to enter command mode\n");
            }

            if (comPort.IsOpen)
                comPort.Close();
        }

        private void BUT_loadcustom_Click(object sender, EventArgs e)
        {
            UploadFW(true);
        }

        private void Progressbar_Click(object sender, EventArgs e)
        {
            beta = !beta;
            CustomMessageBox.Show("Beta set to " + beta.ToString());
        }

        private void linkLabel_mavlink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            S6.SelectedValue = 1;
            S15.Text = (131).ToString();

            RS6.SelectedValue = 1;
            RS15.Text = (131).ToString();

        }

        private void linkLabel_lowlatency_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            S6.SelectedValue = 2;
            S15.Text = (33).ToString();

            RS6.SelectedValue = 2;
            RS15.Text = (33).ToString();
        }
    }
}