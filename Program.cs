using System;
using System.IO;
using System.Threading;

namespace ConsoleApplication1
{
    delegate void EventHandler();
    class Program
    {

        static void HandleEvent() { Console.WriteLine("Event Handled"); }

        static void Main(string[] args)
        {
            try
            {

                EventHandler ev = null;
                ev += HandleEvent;

                CPU.CPU a = CPU.CPU.CPUFactory();
                a.mov(CPU.REG.AX, 100);
                CPU.CPU.ReadAX(a);
                CPU.CPU.DebugRegs(a);
                ev();
                a.div(CPU.REG.AX, 0);
                Console.ReadKey();
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
                Console.ReadKey();
            }
        }
    }
}

namespace CPU {

    public enum REG { AX, BX, CX, DX };
    public class CPU
    {

        private int ax, bx, cx, dx, sp;
        private int[] stk;

        public static CPU CPUFactory()
        {
            return new CPU();
        }

        public CPU()
        {
            stk = new int[1024];
            ax = 0;
            bx = 0;
            cx = 0;
            dx = 0;
            sp = 0;
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
                HardwareInterrupt(0x0C);
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
                HardwareInterrupt(0x0C);
            }
        }

        public void pop(REG r)
        {
            if (sp < 0)
            {
                HardwareInterrupt(0x0C);
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
                HardwareInterrupt(0x00);
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

        public char BIOSInterrupt(byte i)
        {
            switch (i)
            {
                case 0x10:
                    Console.Write(ReadAX(this));
                    return '\0';
                case 0x13:
                    switch(ReadAX(this))
                    {
                        case 0x02:
                            if (!File.Exists(path))
                            {
                                File.CreateText(path);
                                return '\0';
                            }
                            else
                            {
                                using (StreamReader sr = File.OpenText(path))
                                {
                                    for (int k = 0; k < ReadCX(this) - 1; k++) sr.ReadLine();
                                    char j = (char)sr.Read();
                                    return j;
                                }
                            }
                        case 0x03:
                            if (File.Exists(path))
                            {
                                using (StreamWriter sw = new StreamWriter(path))
                                {
                                    sw.WriteLine(ReadBX(this));
                                }
                                return '\0';
                            }
                            else
                            {
                                File.CreateText(path);
                                return '\0';
                            }
                    }
                    return '\0';
                case 0x16:
                    return (char)Console.Read();
                default:
                    return '\0';
            }
        }

        public void HardwareInterrupt(byte i)
        {
            switch (i)
            {
                case 0x00:
                    throw HardwareException("DivisionByZero");
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

        private String path = @"C:\temp\CPU.ram";
    }
}
