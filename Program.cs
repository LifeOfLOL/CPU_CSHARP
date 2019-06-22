using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Threading;
using MySql.Data.MySqlClient;
using System.Windows.Forms;
using System.Collections.Generic;

namespace ConsoleApplication1
{
    delegate Thread ThreadCreationHandler(ThreadStart start);
    delegate void ThreadJoinedHandler(Action a);
    delegate void InterruptHandler(CPU.CPU c, CPU.CPU.INTERRUPT i, int code);
    class Program
    {
        public static event InterruptHandler HandleInterrupt;
        static Thread HandleThreadCreation(ThreadStart start) { return new Thread(start); }
        static void HandleThreadJoin(Action a){ a.Invoke(); }
        static void Main(string[] args)
        {
            try
            {
                HandleInterrupt += (c, intr, code) => { c.Interrupt(intr, code); };

                ThreadCreationHandler th = null;
                th += HandleThreadCreation;

                ThreadJoinedHandler th2 = null;
                th2 += HandleThreadJoin;

                CPU.CPU a = CPU.CPU.CPUFactory();
                a.mov(CPU.REG.AX, 100);
                CPU.CPU.DebugRegs(a);
                ThreadStart l = () => Console.WriteLine("Thread Created");
                Action l2 = () => Console.WriteLine("Thread Joined");
                Thread t = th(l);
                t.Start();
                t.Join();
                th2(l2);
                a.mov(CPU.REG.BX, 10);
                a.mov(CPU.REG.AX, 0x03);
                a.Interrupt(CPU.CPU.INTERRUPT.BIOS, 0x13);
                a.mov(CPU.REG.BX, 0);
                a.mov(CPU.REG.AX, 0x02);
                a.Interrupt(CPU.CPU.INTERRUPT.BIOS, 0x13);
                a.mov(CPU.REG.AX, 10);
                a.Interrupt(CPU.CPU.INTERRUPT.BIOS, 0x05);

                Console.ReadKey();
            }
            catch(CPU.InterruptException e)
            {
                if (HandleInterrupt != null)
                {
                    HandleInterrupt(e.cpu, e.intr, e.code);
                }
            }
            catch(Exception e)
            {
                Console.Write(e.Message);
                Console.ReadKey();
            }
        }
    }
}

namespace CPU
{

    public enum REG { AX, BX, CX, DX };
    public class CPU
    {
        private int ax, bx, cx, dx, sp;
        private int[] stk;
        private MySqlConnection connection;
        private String server;
        private String database;
        private string uid;
        private String password;
        private int id;
        public static CPU CPUFactory()
        {
            return new CPU();
        }
        private void Init()
        {
            server = "localhost";
            database = "RAM";
            uid = "root";
            password = "root";
            String connectionString = "SERVER=" + server + ";DATABASE=" + database + ";UID=" + uid + ";PASSWORD=" + password + ";";
            connection = new MySqlConnection(connectionString);
            id = 0;
        }
        private bool OpenConnection()
        {
            try
            {
                connection.Open();
                return true;
            }
            catch (MySqlException ex)
            {
                switch (ex.Number)
                {
                    case 0:
                        MessageBox.Show("Cannot connect to server.  Contact administrator");
                        break;

                    case 1045:
                        MessageBox.Show("Invalid username/password, please try again");
                        break;
                }
                return false;
            }
        }
        private bool CloseConnection()
        {
            try
            {
                connection.Close();
                return true;
            }
            catch (MySqlException ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }
        }
        public void Insert(String inst, String val)
        {
            string query = "INSERT INTO ram VALUES(" + inst +", " + val + ")";

            //open connection
            if (this.OpenConnection() == true)
            {
                //create command and assign the query and connection from the constructor
                MySqlCommand cmd = new MySqlCommand(query, connection);

                //Execute command
                cmd.ExecuteNonQuery();

                //close connection
                this.CloseConnection();
            }
        }
        public void Update(int id, int val)
        {
            String query = "UPDATE ram SET value = " + val + " where id = "+ id;

            //Open connection
            if (this.OpenConnection() == true)
            {
                //create mysql command
                MySqlCommand cmd = new MySqlCommand();
                //Assign the query using CommandText
                cmd.CommandText = query;
                //Assign the connection using Connection
                cmd.Connection = connection;

                //Execute query
                cmd.ExecuteNonQuery();

                //close connection
                this.CloseConnection();
            }
        }
        public void Delete(int id)
        {
            string query = "DELETE FROM ram WHERE id=" + id;

            if (this.OpenConnection() == true)
            {
                MySqlCommand cmd = new MySqlCommand(query, connection);
                cmd.ExecuteNonQuery();
                this.CloseConnection();
            }

        }
        public List<string>[] Select(int id)
        {
            {
                string query = "SELECT * FROM ram WHERE id = " + id;

                //Create a list to store the result
                List<string>[] list = new List<string>[2];
                list[0] = new List<string>();
                list[1] = new List<string>();

                //Open connection
                if (this.OpenConnection() == true)
                {
                    //Create Command
                    MySqlCommand cmd = new MySqlCommand(query, connection);
                    //Create a data reader and Execute the command
                    MySqlDataReader dataReader = cmd.ExecuteReader();

                    //Read the data and store them in the list
                    while (dataReader.Read())
                    {
                        list[0].Add(dataReader["id"] + "");
                        list[1].Add(dataReader["value"] + "");
                    }

                    //close Data Reader
                    dataReader.Close();

                    //close Connection
                    this.CloseConnection();

                    //return list to be displayed
                    return list;
                }
                else
                {
                    return list;
                }
            }
        }
        public CPU()
        {
            Init();
            stk = new int[1024];
            ax = 0;
            bx = 0;
            cx = 0;
            dx = 0;
            sp = 0;
        }
        ~CPU()
        {
            CloseConnection();
        }
        public static int ReadAX(CPU c) { return c.ax; }
        public static int ReadBX(CPU c) { return c.bx; }
        public static int ReadCX(CPU c) { return c.cx; }
        public static int ReadDX(CPU c) { return c.dx; }
        public static int ReadSP(CPU c) { return c.sp; }
        public int call(Func<int> f) { return f(); }
        public static void DebugRegs(CPU c)
        {
            Console.WriteLine(ReadAX(c));
            Console.WriteLine(ReadBX(c));
            Console.WriteLine(ReadCX(c));
            Console.WriteLine(ReadDX(c));
            Console.WriteLine(ReadSP(c));
        }
        public void push(int val)
        {
            if (sp < 1024)
            {
                stk[sp++] = val;
            }
            else
            {
                Interrupt(INTERRUPT.HARDWARE, 0x0C);
            }
        }
        public void push(REG r)
        {
            if (sp < 1024)
            {
                switch (r)
                {
                    case REG.AX:
                        stk[sp++] = ax;
                        break;
                    case REG.BX:
                        stk[sp++] = bx;
                        break;
                    case REG.CX:
                        stk[sp++] = cx;
                        break;
                    case REG.DX:
                        stk[sp++] = dx;
                        break; ;
                }
            }
            else
            {
                Interrupt(INTERRUPT.HARDWARE, 0x0C);
            }
        }
        public void pop(REG r)
        {
            if (sp < 0)
            {
                Interrupt(INTERRUPT.HARDWARE, 0x0C);
            }
            switch (r)
            {
                case REG.AX:
                    ax = stk[--sp];
                    break;
                case REG.BX:
                    bx = stk[--sp];
                    break;
                case REG.CX:
                    cx = stk[--sp];
                    break;
                case REG.DX:
                    dx = stk[--sp];
                    break;
            }
        }
        public void mov(REG r, int val)
        {
            switch (r)
            {
                case REG.AX:
                    ax = val;
                    break;
                case REG.BX:
                    bx = val;
                    break;
                case REG.CX:
                    cx = val;
                    break;
                case REG.DX:
                    dx = val;
                    break;
            }
        }
        public void add(REG r, int val)
        {
            switch (r)
            {
                case REG.AX:
                    ax += val;
                    break;
                case REG.BX:
                    bx += val;
                    break;
                case REG.CX:
                    cx += val;
                    break;
                case REG.DX:
                    dx += val;
                    break;
            }
        }
        public void sub(REG r, int val)
        {
            switch (r)
            {
                case REG.AX:
                    ax -= val;
                    break;
                case REG.BX:
                    bx -= val;
                    break;
                case REG.CX:
                    cx -= val;
                    break;
                case REG.DX:
                    dx -= val;
                    break;
            }
        }
        public void mul(REG r, int val)
        {
            switch (r)
            {
                case REG.AX:
                    ax *= val;
                    break;
                case REG.BX:
                    bx *= val;
                    break;
                case REG.CX:
                    cx *= val;
                    break;
                case REG.DX:
                    dx *= val;
                    break;
            }
        }
        public void div(REG r, int val)
        {
            if (val == 0)
            {
                Interrupt(INTERRUPT.HARDWARE, 0x00);
            }
            switch (r)
            {
                case REG.AX:
                    ax /= val;
                    break;
                case REG.BX:
                    bx /= val;
                    break;
                case REG.CX:
                    cx /= val;
                    break;
                case REG.DX:
                    dx /= val;
                    break;
            }
        }
        public enum INTERRUPT { BIOS, HARDWARE }
        public void Interrupt(INTERRUPT i, int code)
        {
            switch (i)
            {
                case INTERRUPT.BIOS:
                    Console.Write("BIOS Interrupt: ");
                    BIOSInterrupt(code);
                    break;
                case INTERRUPT.HARDWARE:
                    Console.Write("Hardware Interrupt: ");
                    HardwareInterrupt(code);
                    break;
                default:
                    break;
            }
        }
        private int BIOSInterrupt(int i)
        {
            switch (i)
            {
                case 0x10:
                    Console.Write(ReadAX(this));
                    return 0;
                case 0x13:
                    switch (ReadAX(this))
                    {
                        case 0x02:
                            Console.WriteLine(Select(ReadBX(this)));
                            break;
                        case 0x03:
                            Insert(id++.ToString(), ReadBX(this).ToString());
                            break;
                        case 0x04:
                            Update(ReadAX(this), ReadBX(this));
                            break;
                        case 0x05:
                            Delete(ReadAX(this));
                            break;
                    }
                    break;
                case 0x16:
                    return (char)Console.Read();
                default:
                    return -1;
            }
            return -1;
        }
        private void HardwareInterrupt(int i)
        {
            switch (i)
            {
                case 0x00:
                    throw HardwareException("ZeroDivision");
                case 0x02:
                    throw HardwareException("NMI");
                case 0x0C:
                    throw HardwareException("StackFault");
                default:
                    Console.WriteLine("UnimplementedException");
                    break;
            }
        }
        public Exception HardwareException(string v)
        {
            throw new Exception(v);
        }
        public Exception SoftwareException(String v)
        {
            throw new Exception(v);
        }
    }
    public class InterruptException : Exception
    {
        public CPU.INTERRUPT intr;
        public int code;
        public CPU cpu;
        InterruptException(string msg, CPU cp, CPU.INTERRUPT i, int c) : base(msg)
        {
            cpu = cp;
            intr = i;
            code = c;
        }
    }
}
