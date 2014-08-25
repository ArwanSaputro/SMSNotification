using GsmComm.GsmCommunication;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Configuration.Internal;
using System.IO;
using System.Windows.Forms;
using GsmComm.PduConverter;

namespace SMSNotification
{
    public partial class Service1 : ServiceBase
    {
        public Service1()
        {
            InitializeComponent();
        }

        public BSOB.Service1SoapClient bsob = new BSOB.Service1SoapClient();

        private int port = Properties.Settings.Default.port;
        private int baudrate = Properties.Settings.Default.baudrate;
        private int timeout = Properties.Settings.Default.timeout;
        private string strMessage = Properties.Settings.Default.msg;
        private GsmCommMain Gsm;

        protected System.Timers.Timer timer1 = new System.Timers.Timer();
        protected string errorLog = Path.GetDirectoryName(Application.ExecutablePath) + "\\ErrorLog.txt";
        protected int timeInterval = Properties.Settings.Default.interval;        

        protected override void OnStart(string[] args)
        {
            try
            {
                timer1.Elapsed += timer1_Elapsed;
                timer1.Interval = timeInterval;
                timer1.Enabled = true;

                Gsm.MessageSendComplete += Gsm_MessageSendComplete;
            }

            catch (Exception ex)
            {
                LogError(ex.Message);
            }
        }

        void Gsm_MessageSendComplete(object sender, MessageEventArgs e)
        {
            
        }

        private string sLogFormat;
        private string sErrorTime;

        public void LogError(string sErrMsg)
        {
            try
            {
                sLogFormat = DateTime.Now.ToShortDateString().ToString() + " " + DateTime.Now.ToLongTimeString().ToString() + " ==> ";

                string sYear = DateTime.Now.Year.ToString();
                string sMonth = DateTime.Now.Month.ToString();
                string sDay = DateTime.Now.Day.ToString();
                sErrorTime = sYear + sMonth + sDay;

                FileInfo fi = new FileInfo(errorLog);

                StreamWriter sw = new StreamWriter(fi.DirectoryName + "\\" + sErrorTime + "_" + fi.Name, true);
                sw.WriteLine(sLogFormat + sErrMsg);
                sw.Flush();
                sw.Close();
            }
            catch { }
        }


        void timer1_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                timer1.Stop();

                port = Convert.ToInt32(port);
                baudrate = Convert.ToInt32(baudrate);
                timeout = Convert.ToInt32(timeout);
                if (Connect(port, baudrate, timeout))
                {

                    BSOB.Transaction[] transactions = bsob.GetAllTransaction();
                    
                    foreach (BSOB.Transaction transaction in transactions)
                    {
                        string Id = transaction.ID;
                        string PhoneNo = transaction.PHONENO;
                        DateTime TrxDate = transaction.TRXDATE;

                        string strMsg = strMessage.Replace("@Id", Id).Replace("@TrxDate", TrxDate.ToString("dd/MM/yyyy"));

                        SendSms(PhoneNo, strMsg);

                        bsob.SetSmsNotificationStatus(Id);
                    }
                    //GetAllMessagesFromAllStorage();
                    Gsm.Close();
                }
                
            }
            catch (Exception ex)
            {
                LogError(ex.Message);

            }
            finally
            {
                timer1.Start();
            }

        }

        protected override void OnStop()
        {

        }


        public bool Connect(int comPort, int baudRate, int timeout)
        {
            try
            {
                Gsm = new GsmCommMain(comPort, baudRate, timeout);

                if (!Gsm.IsOpen())
                    Gsm.Open();

                return true;
            }
            catch (System.Exception ex)
            {
                LogError(ex.Message);
                return false;
            }
        }

        public void SendSms(string phoneNo, string msg)
        {

            try
            {
                if ((Gsm == null) || (!Gsm.IsOpen()))
                {
                    return;
                }

                SmsSubmitPdu pdu = new SmsSubmitPdu(msg, phoneNo, String.Empty);
                Gsm.SendMessage(pdu);
                //after sms set status notify here                
            }
            catch (Exception ex)
            {
                LogError(ex.Message);
            }
        }

        
        private string GetMessageStorage(MessageLocation Location)
        {
            string storage = string.Empty;
            if (Location == MessageLocation.Sim)
                storage = PhoneStorageType.Sim;
            else
                storage = PhoneStorageType.Phone;

            if (storage.Length == 0)
                throw new ApplicationException("Unknown message storage.");
            else
                return storage;
        }

        enum MessageLocation
        {
            Sim = 0,
            Phone = 1
        }

        private void GetAllMessagesFromAllStorage()
        {
            try
            {
                if (Gsm.IsConnected() && Gsm.IsOpen())
                {
                    DecodedShortMessage[] SimMessages = Gsm.ReadMessages(PhoneMessageStatus.All, GetMessageStorage(MessageLocation.Sim));
                    Gsm.DeleteMessages(DeleteScope.All, GetMessageStorage(MessageLocation.Sim));

                    foreach (DecodedShortMessage message in SimMessages)
                    {
                        SmsPdu pdu = (SmsPdu)message.Data;

                        if (pdu is SmsDeliverPdu)
                        {
                            // Received message
                            SmsDeliverPdu data = (SmsDeliverPdu)pdu;

                            string msg = data.UserDataText;
                            DateTime receive_date = data.SCTimestamp.ToDateTime();                
                            string phone_num = data.OriginatingAddress;

                            //insert statement here
                        }
                    }

                    DecodedShortMessage[] PhoneMessages = Gsm.ReadMessages(PhoneMessageStatus.All, GetMessageStorage(MessageLocation.Phone));
                    Gsm.DeleteMessages(DeleteScope.All, GetMessageStorage(MessageLocation.Phone));


                    foreach (DecodedShortMessage message in PhoneMessages)
                    {
                        SmsPdu pdu = (SmsPdu)message.Data;

                        if (pdu is SmsDeliverPdu)
                        {
                            // Received message
                            SmsDeliverPdu data = (SmsDeliverPdu)pdu;

                            string pesan = data.UserDataText;
                            DateTime tgl_terima = data.SCTimestamp.ToDateTime();                            
                            string phone_num = data.OriginatingAddress;

                            //insert statement here                            
                        }
                    }
                }
            }
            catch (Exception ex)
            {


                LogError("GetAllMessagesFromAllStorage " + ex.Source + " " + ex.Message);

            }

        }

        private void GetAllMessagesByStorage(int storage)
        {
            try
            {
                if (Gsm.IsConnected() && Gsm.IsOpen())
                {
                    DecodedShortMessage[] messages = Gsm.ReadMessages(PhoneMessageStatus.All, GetMessageStorage((MessageLocation)storage));
                    Gsm.DeleteMessages(DeleteScope.All, GetMessageStorage((MessageLocation)storage));

                    foreach (DecodedShortMessage message in messages)
                    {

                        SmsPdu pdu = (SmsPdu)message.Data;

                        if (pdu is SmsDeliverPdu)
                        {
                            // Received message
                            SmsDeliverPdu data = (SmsDeliverPdu)pdu;

                            string msg = data.UserDataText;
                            DateTime receive_date = data.SCTimestamp.ToDateTime();                            
                            string phone_num = data.OriginatingAddress;

                            //insert statement here                            
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                LogError("GetAllMessagesByStorage " + ex.Source + " " + ex.Message);
            }

        }

        void GsmComm_PhoneDisconnected(object sender, EventArgs e)
        {
            try
            {


            }
            catch (Exception ex)
            {
                LogError("GsmComm_PhoneDisconnected " + ex.Source + " " + ex.Message);
            }

        }

        void GsmComm_PhoneConnected(object sender, EventArgs e)
        {
            try
            {


            }
            catch (Exception ex)
            {
                LogError("GsmComm_PhoneConnected " + ex.Source + " " + ex.Message);
            }

        }


    }
}
