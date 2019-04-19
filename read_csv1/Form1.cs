using System;
using System.Windows.Forms;
using System.IO.Ports;
using System.Timers;
using System.Text;
using System.Threading;
using System.Windows.Forms.DataVisualization.Charting;
using System.Collections.Concurrent;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace read_csv1
{

    public partial class Form1 : Form
    {
        SerialPort port_xx;
        SerialPort port_T;
        public byte cs;
        int rawdata = 0;
        double raw = 0;
        int index = 0;
        int[] buffer_x = new int[6] { 70000, 70000, 70000, 70000, 70000, 70000 };
        int[] buffer_y = new int[6] { 70000, 70000, 70000, 70000, 70000, 70000 };
        int buffer_control = 0;

        StringBuilder stringBuilder = new StringBuilder(2048);
        BlockingCollection<int> dataItems = new BlockingCollection<int>(500);
        private readonly Mutex mstringBuilderMutex = new Mutex();
        System.Timers.Timer readBufferTimer = new System.Timers.Timer(1000);

        public Form1()
        {
            InitializeComponent();
            InitializeGraphs(chart1, SeriesChartType.FastLine, -0.5, 0.8);


        }
        private void InitializeGraphs(Chart chart, SeriesChartType type, double yMin, double yMax)
        {
            chart.Legends.Clear();

            var series = chart.Series[0];
            if (chart.Series.Count > 1)
                chart.Series[1].ChartType = SeriesChartType.Point;
            var chartArea = chart.ChartAreas[0];

            series.ChartType = type;


            series.XValueType = ChartValueType.Int32;

            chartArea.AxisX.ScrollBar.Enabled = true;
            chartArea.AxisX.ScrollBar.IsPositionedInside = false;

            chartArea.AxisY.ScaleView.Size = 20;
            chartArea.AxisX.ScaleView.Size = 2000;
            //chartArea.AxisX.MajorTickMark.Interval = 1250;
            //chartArea.AxisX.MajorGrid.Interval = 1250;
            chartArea.CursorX.AutoScroll = true;
            chartArea.CursorX.IsUserEnabled = true;
            chartArea.CursorX.IsUserSelectionEnabled = true;
            chartArea.AxisX.ScaleView.Zoomable = true;

            chartArea.AxisX.Minimum = 0;

            //chartArea.AxisY.Minimum = yMin;
            //chartArea.AxisY.Maximum = yMax;
        }
        private void Change_Yscale(Chart chart, int Yscale)
        {
            var chartArea = chart.ChartAreas[0];
            chartArea.AxisY.ScaleView.Size = Yscale;
        }
        private void label1_Click(object sender, EventArgs e)
        {

        }

        public delegate void SetTextDeleg(String s);
        public delegate void SetDlgDeleg(int x, double y);
        public delegate void ClearDlgDeleg();
        public void show_data(byte[] data, int bytes_number)
        {

            textBox1.AppendText(bytes_number.ToString() + Environment.NewLine);
            //textBox1.AppendText((BitConverter.ToString(data)) + Environment.NewLine);
        }

        int check = 0, i = 0, mark = 0;
        byte[] payload = new byte[4];
        byte paylength;

        int k = 0;
        byte[] buffer_T = new byte[8];

        void xx_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {

            if (!port_xx.IsOpen) return;
            //int bytes_number = port_xx.BytesToRead;

            int bytes_number = 0;
            if (port_xx.BytesToRead >= 64)
                bytes_number = 64;
            byte[] buffer = new byte[bytes_number];
            port_xx.Read(buffer, 0, bytes_number);


            int j = 0;

            while (bytes_number > 0)
            {
                bytes_number--;
                // textBox1.AppendText(bytes_number.ToString() + Environment.NewLine);
                byte temp = buffer[j++];

                if (temp == 0xBB && k == 0)
                {
                    buffer_T[0] = temp;
                    k++;
                }
                else if (k != 0 && k < 8)
                    buffer_T[k++] = temp;
                else if (k == 8)
                {

                    int p = 0;
                    while (buffer_T[p] == 0xBB)
                    {
                        p++;
                    }
                    if (p == 6)
                    {
                        byte[] Payload_T = new byte[2];
                        string T_str;
                        double Tnow = 0;
                        int radio = 0;
                        Payload_T[0] = buffer_T[6];
                        Payload_T[1] = buffer_T[7];
                        radio = (Payload_T[0] << 8) | Payload_T[1];
                        int Rref = 158;
                        double R = (double)radio * Rref / (65535 - radio);
                        double R0 = 159;
                        double T0 = 20;
                        double change_radio = 0.5;
                        Tnow = T0 + (R - R0) / change_radio;
                        T_str = Tnow.ToString() + "     ";
                        BeginInvoke(new SetTextDeleg((String str) =>
                        {
                            textBox1.Text = Tnow.ToString("0.00");
                            //textBox1.Text = buffer[0].ToString();
                            //textBox4.Text = buffer[1].ToString();
                        }), new object[] { T_str });
                    }
                    k = 0;
                }

                if (check == 0 && temp == 0xaa)
                {
                    check = 1;
                    continue;
                }
                if (check == 1)
                {
                    if (temp == 0xaa)
                    {
                        check = 2;
                        continue;
                    }
                    else
                    {
                        check = 0;
                        continue;
                    }
                }
                if (check == 2)
                {
                    paylength = temp;
                    if (paylength == 0x04)
                    {
                        check = 3;
                        continue;
                    }
                    else
                    {
                        check = 0;
                        continue;
                    }
                }
                if (check >= 3)
                {
                    if (i < 4)
                    {
                        payload[i++] = temp;
                    }
                    else
                    {
                        cs = temp;
                        i = 0; check = 0; mark = 1;
                    }
                }

                if (i == 0 && check == 0 && mark == 1)
                {
                    int bytecount = 0;
                    byte checksum = 0x00;
                    byte code, length;

                    for (int k = 0; k < 4; k++)
                        checksum += payload[k];

                    checksum = (byte)(~(int)(checksum & 0xFF));
                    if (checksum != cs)
                        continue;
                    else
                    {
                        code = payload[bytecount++];
                        if (((int)code & 0x80) != 0) length = payload[bytecount++];
                        else length = 1;

                        rawdata = (payload[bytecount] << 8 | payload[bytecount + 1]);
                        if (rawdata >= 32768) rawdata = rawdata - 65536;


                        buffer_x[buffer_control] = rawdata;

                        if ((buffer_x[(buffer_control + 1) % 6] != 70000))
                        {
                            buffer_y[buffer_control] = buffer_y[(buffer_control + 5) % 6] + (buffer_x[buffer_control] - buffer_x[(buffer_control + 1) % 6]) / 5;
                        }
                        else
                        {
                            buffer_y[buffer_control] = buffer_x[buffer_control];
                        }



                        // raw = rawdata * 18.3 / 128.0;

                        if (!dataItems.IsAddingCompleted)
                            dataItems.Add(rawdata);
                            //dataItems.Add(buffer_y[buffer_control]);
                        buffer_control = (buffer_control + 1) % 6;
                    }
                    mark = 0;
                }
            }
        }



        public void DataChart(TaskScheduler uiScheduler)
        {
            foreach (var data in dataItems.GetConsumingEnumerable())
            //               BeginInvoke(new SetTextDeleg((String str) => {
            //                 textBox1.AppendText(str);
            //             }), new object[] { index +"\n" });
            //         index++;
            {
                if ((index++) % 2000 == 0)
                    BeginInvoke(new ClearDlgDeleg(() =>
                    {
                        chart1.Series[0].Points.Clear();
                    }), new object[] { });
                //double data_show = data * 18.3 / 51200.0;
                double data_show = data / 3000.0;
                BeginInvoke(new SetDlgDeleg((int x, double y) =>
                {
                    chart1.Series[0].Points.AddXY(x, y);
                }), new object[] { (index) % 2000, data_show });
            }
        }
        private void textBox2_TextChanged(object sender, EventArgs e) { }
        private void TextBox2_Validated(object sender, EventArgs e)
        {

            if (!string.IsNullOrEmpty(Y_Range.Text))
            {
                //textBox1.AppendText("invalid textbox value, could not be null");
                try
                {
                    int A = int.Parse(Y_Range.Text);
                    Change_Yscale(chart1, A);
                    textBox1.AppendText("ok\n");
                }
                catch
                {
                    textBox1.AppendText("invalid textbox value\n");
                }

            }


        }

        private void button1_Click(object sender, EventArgs e)
        {
            string com_number = Com_ECG.Text;
            port_xx = new SerialPort(com_number, 57600);
            TaskScheduler uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            Task.Factory.StartNew(() => DataChart(uiScheduler));
            try
            {
                port_xx.Open();
                port_xx.DataReceived += xx_DataReceived;

            }
            catch
            {
                label_T.Text = "No" + com_number;
            }

        }

        void xx_DataReceived_T(object sender, SerialDataReceivedEventArgs e)
        {
            if (!port_T.IsOpen) return;
            int bytes_number = port_T.BytesToRead;
            //int bytes_number = 2;
            byte[] buffer = new byte[bytes_number];

            string T_str;
            double Tnow=0;
            int radio = 0;
            if (bytes_number >= 2)
            {
                port_T.Read(buffer, 0, bytes_number);
                radio = (buffer[0] << 8) | buffer[1];
                int Rref = 158;
                double R = (double)radio * Rref / (65535 - radio);
                double R0 = 159;
                double T0 = 20;
                double change_radio = 0.5;
                Tnow = T0 + (R - R0) / change_radio;
                T_str = Tnow.ToString() + "     ";
                BeginInvoke(new SetTextDeleg((String str) =>
                {
                    textBox1.Text = Tnow.ToString("0.00");
                    //textBox1.Text = buffer[0].ToString();
                    //textBox4.Text = buffer[1].ToString();
                }), new object[] { T_str });
            }



        }
        /*
        private void button2_Click(object sender, EventArgs e)
        {
            string com_number = Com_T.Text;
            port_T = new SerialPort(com_number, 57600);

            try
            {
                port_T.Open();
                port_T.DataReceived += xx_DataReceived_T;

            }
            catch
            {
                label_T.Text = "No" + com_number;
            }
        }
        */

    }
}
