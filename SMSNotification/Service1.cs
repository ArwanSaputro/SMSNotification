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
        private BSOB.Products[] prods = null;

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

                //if (Gsm.IsConnected() && Gsm.IsOpen())
                //    Gsm.Close();
            }
        }

        void Gsm_MessageSendComplete(object sender, MessageEventArgs e)
        {
            
        }

        private string sLogFormat;
        private string sErrorTime;


        public void ProcessAllMessage()
        {
            try
            {
                BSOB.SMS[] allSms = bsob.SMSGetAll();

                foreach (BSOB.SMS sms in allSms)
                {
                    Order(sms);
                    bsob.SetReplyStatus(sms.ID);
                }
            }
            catch (Exception ex) 
            {
                LogError(ex.Message);

                if (Gsm.IsConnected() && Gsm.IsOpen())
                    Gsm.Close();
            }
        }

        private void Order(BSOB.SMS sms)
        {
            try
            {
                string msg = sms.MESSAGE;

                string[] msgSplit = msg.Split(new char[] { '#' });

                if (msgSplit[0].ToString().ToLower().StartsWith("pesan"))
                {
                    string QRCode = msgSplit[1].ToString().ToLower();
                    string StoreId = "";

                    if (!ValidateQRCode(QRCode, out StoreId))
                    {
                        SendSms(sms.PHONENO, "Toko anda belum terdaftar.");
                        //send sms invalid qrcode..
                        return;
                    }
                    DateTime deliveryDate = new DateTime();
                    string strdeliveryDate = msgSplit[2].ToString();

                    if (!ValidateDeliveryDate(strdeliveryDate, out deliveryDate))
                    {
                        SendSms(sms.PHONENO, "Format tanggal salah. Silakan gunakan format tgl/bln/thn");
                        //send sms wrong format date..
                        return;
                    }


                    BSOB.OrderDetails[] orderDetail = null;

                    if (!ValidateOrderDetails(msgSplit, out orderDetail))
                    {
                        string strListProduct = "";

                        BSOB.Products[] prods = bsob.GetProduct();
                        foreach (BSOB.Products prod in prods)
                        {
                            strListProduct += prod.PRODUCTCODE + ", ";
                        }
                        strListProduct = strListProduct.Substring(0, strListProduct.Length - 2);

                        SendSms(sms.PHONENO, "Kode produk tidak terdaftar. Kode produk yang benar adalah " + strListProduct);
                        //send error product not exists..
                        return;
                    }

                    string OrderId = "";
                    bsob.InsertOrder(StoreId, sms.PHONENO, deliveryDate, orderDetail, out OrderId);
                    SendSms(sms.PHONENO, "Terima kasih anda telah melakukan pemesanan produk Bintang Sobo. Data pemesanan anda telah kami simpan dengan No " + OrderId);
                }
                else
                {
                    //send error message not valid..
                    SendSms(sms.PHONENO, "Input salah. Format pemesanan: pesan#qrcode#tgl/bln/thn#kodeproduk1#jumlah1#kodeproduk2#jumlah2#kodeproduk3#jumlah3. Contoh: pesan#TokoABC#01/08/2014#TK300A#10");
                }
            }
            catch (Exception ex)
            {
                LogError(ex.Message);

                if (Gsm.IsConnected() && Gsm.IsOpen())
                    Gsm.Close();
            }
        }


        private bool ValidateOrderDetails(string[] msgSplit, out BSOB.OrderDetails[] orderDetail)
        {
            try
            {
                                
                int totalProduct = (msgSplit.Length - 3) / 2;

                orderDetail = new BSOB.OrderDetails[totalProduct];
                int i = 0;
                for (int idx = 3; idx < msgSplit.Length; idx++)
                {
                    if (msgSplit[idx].ToString().ToLower() != "")
                    {
                        string ProductId = "";

                        string ProductCode = msgSplit[idx].ToString().ToLower();
                        if (IsExistingProduct(ProductCode, out ProductId))
                        {
                            orderDetail[i] = new BSOB.OrderDetails();
                            orderDetail[i].PRODUCTID = ProductId;

                            orderDetail[i].PRODUCTCODE = ProductCode;

                            idx += 1;
                            orderDetail[i].QUANTITY = Convert.ToInt32(msgSplit[idx].ToString());

                            i += 1;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }


                return true;
            }
            catch(Exception ex)
            {
                LogError(ex.Message);

                if (Gsm.IsConnected() && Gsm.IsOpen())
                    Gsm.Close();

                orderDetail = null;
                return false;
            }
        }

        public bool IsExistingProduct(string ProductCode, out string ProductId)
        {
            ProductId = "";
            try
            {
                foreach (BSOB.Products prod in prods)
                {
                    if (prod.PRODUCTCODE.ToLower() == ProductCode.ToLower())
                    {
                        ProductId = prod.ID;
                        return true;
                    }
                }

                return false;
            }
            catch(Exception ex)
            {
                LogError(ex.Message);

                if (Gsm.IsConnected() && Gsm.IsOpen())
                    Gsm.Close();

                return false;
            }
        }

        private bool ValidateQRCode(string QRCode, out string StoreId)
        {
            StoreId = "";
            try
            {
                BSOB.Stores store = bsob.GetStoreByQRCode(QRCode);

                if (store != null)
                {
                    StoreId = store.ID;
                    return true;
                }
                else
                    return false;                
            }
            catch(Exception ex)
            {
                LogError(ex.Message);

                if (Gsm.IsConnected() && Gsm.IsOpen())
                    Gsm.Close();

                return false;
            }
        }

        private bool ValidateDeliveryDate(string strDate, out DateTime deliveryDate)
        {
            deliveryDate = DateTime.MinValue;
            string[] strSplitDate = strDate.Split(new char[] { '/' });
            try
            {
                int day = Convert.ToInt32(strSplitDate[0].ToString());
                int month = Convert.ToInt32(strSplitDate[1].ToString());
                int year = Convert.ToInt32(strSplitDate[2].ToString());
                deliveryDate = new DateTime(year, month, day);

                return true;
            }
            catch(Exception ex)
            {
                LogError(ex.Message);

                if (Gsm.IsConnected() && Gsm.IsOpen())
                    Gsm.Close();

                return false;
            }
        }

        public void LogError(string sErrMsg)
        {
            try
            {
                sLogFormat = DateTime.Now.ToShortDateString().ToString() + " " + DateTime.Now.ToLongTimeString().ToString() + " ==> ";

                string sYear = DateTime.Now.Year.ToString();
                string sMonth = "0" + DateTime.Now.Month.ToString();
                string sDay = "0" + DateTime.Now.Day.ToString();
                sErrorTime = sYear + sMonth.Substring(sMonth.Length - 2, 2) + sDay.Substring(sDay.Length - 2, 2);

                

                FileInfo fi = new FileInfo(errorLog);

                StreamWriter sw = new StreamWriter(fi.DirectoryName + "\\" + sErrorTime + "_" + fi.Name, true);
                sw.WriteLine(sLogFormat + sErrMsg);
                sw.Flush();
                sw.Close();
            }
            catch 
            {
                if (Gsm.IsConnected() && Gsm.IsOpen())
                    Gsm.Close();
            }
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
                    prods = bsob.GetProduct();
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
                    
                    GetAllMessagesFromAllStorage();

                    ProcessAllMessage();

                    if (Gsm.IsConnected() && Gsm.IsOpen())
                        Gsm.Close();
                }
                
            }
            catch (Exception ex)
            {
                LogError(ex.Message);

                if (Gsm.IsConnected() && Gsm.IsOpen())
                    Gsm.Close();
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

                if (Gsm.IsConnected() && Gsm.IsOpen())
                    Gsm.Close();

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

                if (Gsm.IsConnected() && Gsm.IsOpen())
                    Gsm.Close();
            }
        }

        
        private string GetMessageStorage(MessageLocation Location)
        {
            string storage = string.Empty;

            try
            {
                
                if (Location == MessageLocation.Sim)
                    storage = PhoneStorageType.Sim;
                else
                    storage = PhoneStorageType.Phone;

                if (storage.Length == 0)
                    throw new ApplicationException("Unknown message storage.");
                else
                    return storage;
            }
            catch (Exception ex)
            {
                LogError(ex.Message);

                if (Gsm.IsConnected() && Gsm.IsOpen())
                    Gsm.Close();

                return storage;
            }
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

                            string sim_msg = data.UserDataText;
                            DateTime sim_receive_date = data.SCTimestamp.ToDateTime();                
                            string sim_num = data.OriginatingAddress;
                            
                            //BSOB.SMS sms = new BSOB.SMS();
                            //sms.MESSAGE = sim_msg;
                            //sms.PHONENO = sim_num;
                            //sms.RECEIVEDDATE = sim_receive_date;
                            //Order(sms);

                            bsob.SMSQueuing(sim_msg, sim_num, sim_receive_date);         
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

                            string phone_msg = data.UserDataText;
                            DateTime phone_received_date = data.SCTimestamp.ToDateTime();                            
                            string phone_num = data.OriginatingAddress;

                            //BSOB.SMS sms = new BSOB.SMS();
                            //sms.MESSAGE = phone_msg;
                            //sms.PHONENO = phone_num;
                            //sms.RECEIVEDDATE = phone_received_date;
                            //Order(sms);
                           
                            bsob.SMSQueuing(phone_msg, phone_num, phone_received_date);
                            //insert statement here                            
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("GetAllMessagesFromAllStorage " + ex.Source + " " + ex.Message);

                if (Gsm.IsConnected() && Gsm.IsOpen())
                    Gsm.Close();
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

                if (Gsm.IsConnected() && Gsm.IsOpen())
                    Gsm.Close();
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

                if (Gsm.IsConnected() && Gsm.IsOpen())
                    Gsm.Close();
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

                if (Gsm.IsConnected() && Gsm.IsOpen())
                    Gsm.Close();
            }

        }


    }
}
