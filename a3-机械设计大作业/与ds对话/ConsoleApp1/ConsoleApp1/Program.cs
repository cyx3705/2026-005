using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Text.Json;

class Program
{
    public static string apiKey = "sk-3e27c806dfe4464287f73ac01d598de1";



    static void Main()
    {
        bool continue1=true;
        string us = null;       //使用工况
        float h = 0;            //每日使用时间
        float P = 0;            //电机功率
        float n = 0;            //转速
        float i = 0;            //传动比
        float Pca = 0;          //计算功率
        float Ka = 0;           //工作情况系数
        string Belt = null;     //选v带
        float dD1 = 0;          //小带轮
        float dD2c = 0;         //大带轮
        float dD2 = 0;
        float v = 0;            //验算
        string vConditon = null;
        float a0 = 0;           //初定中心距     
        float Ld0 = 0;          //所需基准长度
        float Ld = 0;           //基准长度
        float a = 0;            //实际中心距
        float amin = 0;         //中心距离变化范围
        float amax = 0;         //中心距离变化范围    
        float α = 0;           //验算小带轮包角
        string αConditon = null;
        float αfitting= 0;
        float P0 = 0;           //基本额定功率
        float ΔP0 = 0;         //基本额定功率增量
        float Kα = 0;          //包角修正系数
        float KL = 0;           //修正系数
        float Pr = 0;           //额定功率
        int z = 0;            //v带根数
        float q = 0;            //带单位长度质量
        float F0 = 0;           //初拉力
        float Fp = 0;           //压轴力
        Console.WriteLine("作业要求填什么就填什么(本作业的目的在于希望使用大模型的能力处理在机械设计过程中有主观参与的部分，避免查表去试的时候累死)");
        while (continue1 == true)
        {
            string[] get = input();
            if (get[0]=="exit")
            {
                continue1 = false;
            }
            if (get[0] != "go")
            {
                if (get[0] == "P")
                {
                    if (get.Length > 2 || get.Length < 2)
                    {
                        Console.WriteLine("输入了错误的参数");
                    }
                    else
                    {
                        P = float.Parse(get[1]);
                    }
                }
                if (get[0] == "n")
                {
                    if (get.Length > 2 || get.Length < 2)
                    {
                        Console.WriteLine("输入了错误的参数");
                    }
                    else
                    {
                        n = float.Parse(get[1]);
                    }
                }
                if (get[0]=="us")
                {
                    if (get.Length > 2 || get.Length < 2)
                    {
                        Console.WriteLine("输入了错误的参数");
                    }
                    else
                    {
                        us=get[1];
                    }
                }
                if (get[0]=="h")
                {
                    if (get.Length > 2 || get.Length < 2)
                    {
                        Console.WriteLine("输入了错误的参数");
                    }
                    else
                    {
                        h = float.Parse(get[1]);
                    }
                }
                if (get[0]=="i")
                {
                    if (get.Length > 2 || get.Length < 2)
                    {
                        Console.WriteLine("输入了错误的参数");
                    }
                    else
                    {
                        i = float.Parse(get[1]);
                    }
                }
                if (get[0] == "P0")
                {
                    if (get.Length > 2 || get.Length < 2)
                    {
                        Console.WriteLine("输入了错误的参数");
                    }
                    else
                    {
                        P0 = float.Parse(get[1]);
                    }
                }
                if (get[0] == "ΔP0")
                {
                    if (get.Length > 2 || get.Length < 2)
                    {
                        Console.WriteLine("输入了错误的参数");
                    }
                    else
                    {
                        ΔP0 = float.Parse(get[1]);
                    }
                }
                else if(get[0] != "h"&& get[0] != "us"&& get[0] != "P"&& get[0] != "n"&& get[0] != "i" && get[0] != "P0" && get[0] != "ΔP0")
                {
                    Console.WriteLine("未知命令，请输入正确的指令");
                }
            }

            if (h!=0&&us!=null&&Ka==0)
            {
                Ka = getKa(us, h);
            }
            if (P!=0&&Ka!=0&&Pca==0)
            {
                Pca = Ka * P;
            }
            if(Pca!=0&&n!=0&&Belt==null)
            {
                Belt = getbelt(Pca, n);
            }
            if (Belt != null&&dD1==0)
            {
                float get1 = getdD1(Belt,vConditon);
                dD1 = get1;
            }
            if(dD1!=0&&i!=0&&dD2c==0)
            {
                dD2c = i * dD1;
            }
            //大带轮的就近求有两种办法，一种使用求差值最小，一种使用提示词工程，下面是使用提示词工程的版本
            if(dD2c!=0&&Belt!=null&&dD2==0)
            {
               float get2=getDd2(Belt,dD2c);
               dD2 = get2;
            }
            if(dD1!=0&&n!=0&&vConditon==null)
            {
                v = ((float)3.14 * dD1 * n) / (60 * 1000);
                if(v > 5f && v < 25f)
                {
                    Console.WriteLine("所需的v符合要求");
                    vConditon = "ok";
                }
                else if(v < 5f)
                {
                    Console.WriteLine("所需的v太小了,重新选小带轮直径");
                    dD1 = 0;
                    vConditon = "small";
                }
                else
                {
                    Console.WriteLine("所需的v太大了，重新选小带轮直径");
                    dD1 = 0;
                    vConditon = "big";
                }
            }
            if(dD1!=0&&dD2!=0&&amax==0&&amin==0)
            {
                amax = 2 * (dD1 + dD2);
                amin = (float)0.7 * (dD1 + dD2);
            }
            if(amax!=0&&amin!=0&&a0==0)
            {
                a0=geta0(amin, amax, dD1, dD2,αConditon);
            }
            if(a0!=0&&dD1!=0&&dD2!=0&&Ld0==0)
            {
                float term1 = (float)Math.Pow(dD2 - dD1, 2);
                Ld0 = 2 * a0 + 1.57f * (dD1 + dD2) + term1/ (4 * a0);
            }
            if(Ld0!=0&&Belt!=null&&Ld==0)
            {
                Ld=getLd(Belt,Ld0);
            }
            if (Ld0 != 0&&a==0)
            {
                a = a0 + (Ld - Ld0) / 2;
            }
            if(a!=0&&dD1!=0&&dD2!=0&& α==0)
            {
                α = 180 - (dD2 - dD1) * 57.3f / a;
                if (α>=120)
                {
                    Console.WriteLine("所需的α符合要求");
                    Console.WriteLine(getlist(P, h, us, n, i, Pca, Ka, Belt, dD1, dD2c, dD2, v, a0, Ld0, Ld, a, amin, amax, α, P0, ΔP0, Kα, KL, Pr, z, q, F0, Fp));
                    αConditon = null;
                }
                else 
                {
                    Console.WriteLine("所需的α太小了,重新选初定中心距");
                    Console.WriteLine(getlist(P, h, us, n, i, Pca, Ka, Belt, dD1, dD2c, dD2, v, a0, Ld0, Ld, a, amin, amax, α, P0, ΔP0, Kα, KL, Pr, z, q, F0, Fp));
                    Ld0 = 0;
                    αConditon = "small";
                }
            }
            if(α!=0&& Kα==0)
            {
                αfitting = getαfitting(α);
                Dictionary<float, float> αDictionary = new Dictionary<float, float>()
                {
                    {180,1},
                    {174,0.99f},
                    {169,0.97f},
                    {163,0.96f},
                    {157,0.94f},
                    {151,0.93f},
                    {145,0.91f},
                    {139,0.89f},
                    {133,0.87f},
                    {127,0.85f},
                    {120,0.82f},
                };
                Kα = αDictionary[αfitting];
            }
            if (Belt != null && Ld != 0&& KL==0) 
            {
                Console.WriteLine(getlist(P, h, us, n, i, Pca, Ka, Belt, dD1, dD2c, dD2, v, a0, Ld0, Ld, a, amin, amax, α, P0, ΔP0, Kα, KL, Pr, z, q, F0, Fp));
                KL =getKL(Belt, Ld);
            }
            if(P0!=0&&ΔP0!=0&&Kα!=0&&KL!=0&& Pr==0)
            {
                Pr = (P0 + ΔP0) * Ka * KL;
            }
            if(Pca!=0&&Pr!=0&&z==0)
            {
                z = (int)(Math.Ceiling(Pca / Pr));
            }
            if (Belt != null&&q==0)
            {
                Dictionary<string, float> qDictionary = new Dictionary<string, float>()
                {
                    {"Z",0.060f},
                    {"A",0.105f},
                    {"B",0.170f},
                    {"C",0.300f},
                    {"D",0.630f},
                };
                q = qDictionary[Belt.Substring(0, 1)];
            }
            if(Pca!=0 && Kα!= 0 && z!=0 && v!=0&& α!=0&&F0==0&&Fp==0)
            {
                F0=(500*(2.5f- Kα) *Pca)/ (Kα*z*v);
                float sin = (float)Math.Sin(α/2);
                Fp = 2 * z * F0 * sin;
            }
            Console.WriteLine(getlist(P,h,us, n, i, Pca, Ka, Belt, dD1, dD2c, dD2, v, a0, Ld0, Ld, a, amin, amax, α, P0, ΔP0, Kα, KL, Pr, z, q, F0, Fp));
        }
    

    }
    static float getKa(string us,float h)
    {
        string getqd1=getqd(us);
        string getgk1=getgk(us);
        if (getqd1 == "轻载启动")
        {
            if (getgk1== "载荷变动微小")
            {
                if(h<10)
                {
                    return 1f;
                }
                else if (h>10&&h<16)
                {
                    return 1.1f;
                }
                else
                {
                    return 1.2f;
                }
            }
            if (getgk1 == "载荷变动小")
            {
                if (h < 10)
                {
                    return 1.1f;
                }
                else if (h > 10 && h < 16)
                {
                    return 1.2f;
                }
                else
                {
                    return 1.3f;
                }
            }
            if (getgk1 == "载荷变动较大")
            {
                if (h < 10)
                {
                    return 1.2f;
                }
                else if (h > 10 && h < 16)
                {
                    return 1.3f;
                }
                else
                {
                    return 1.4f;
                }
            }
            if (getgk1 == "载荷变动很大")
            {
                if (h < 10)
                {
                    return 1.3f;
                }
                else if (h > 10 && h < 16)
                {
                    return 1.4f;
                }
                else
                {
                    return 1.5f;
                }
            }
            else
            {
                return 0;
            }
        }
        if (getqd1 == "重载启动")
        {
            if (getgk1 == "载荷变动微小")
            {
                if (h < 10)
                {
                    return 1.1f;
                }
                else if (h > 10 && h < 16)
                {
                    return 1.2f;
                }
                else 
                {
                    return 1.3f;
                }
            }
            if (getgk1 == "载荷变动小")
            {
                if (h < 10)
                {
                    return 1.2f;
                }
                else if (h > 10 && h < 16)
                {
                    return 1.3f;
                }
                else
                {
                    return 1.4f;
                }
            }
            if (getgk1 == "载荷变动较大")
            {
                if (h < 10)
                {
                    return 1.4f;
                }
                else if (h > 10 && h < 16)
                {
                    return 1.5f;
                }
                else
                {
                    return 1.6f;
                }
            }
            if (getgk1 == "载荷变动很大")
            {
                if (h < 10)
                {
                    return 1.5f;
                }
                else if (h > 10 && h < 16)
                {
                    return 1.6f;
                }
                else
                {
                    return 1.8f;
                }
            }
            else
            {
                return 0;
            }
        }
        else { return 0; }
    }
    static string getqd(string us)
    {
        string userMessage = $"现在要设计一种带轮组，已知带传动运行的工况是{us}，现在请你直接从（轻载启动，重载启动）中选择一个回答该工况载荷变动的可能，不要说理由";
        string response = AskDeepSeek(apiKey, userMessage);
        Console.WriteLine(response);
        string response1 = DeepSeekreason(apiKey, userMessage, response);
        Console.WriteLine(response1);
        return response;
    }
    static string getgk(string us)
    {
        string userMessage = $"现在要设计一种带轮组，已知带传动运行的工况是{us}，现在请你直接从（载荷变动微小，载荷变动小，载荷变动较大，载荷变动很大）中选择一个回答该工况载荷变动的可能，不要说理由";
        string response = AskDeepSeek(apiKey, userMessage);
        Console.WriteLine(response);
        string response1 = DeepSeekreason(apiKey, userMessage, response);
        Console.WriteLine(response1);
        return response;
    }
    static string getlist(float P,            //电机功率
    float h,
    string us,
    float n,            //转速
    float i,            //传动比
    float Pca,          //计算功率
    float Ka,          //工作情况系数
    string Belt,     //选v带
    float dD1,          //小带轮
    float dD2c,         //大带轮
    float dD2,
    float v,            //验算
    float a0,           //初定中心距     
    float Ld0,          //所需基准长度
    float Ld,           //基准长度
    float a,            //实际中心距
    float amin,         //中心距离变化范围
    float amax,         //中心距离变化范围    
    float α,           //验算小带轮包角
    float P0,           //基本额定功率
    float ΔP0,         //基本额定功率增量
    float Kα,          //包角修正系数
    float KL,           //修正系数
    float Pr,           //额定功率
    int z,            //v带根数
    float q,            //带单位长度质量
    float F0,           //初拉力
    float Fp)           //压轴力
    {
        return $"\n输入变量：输入工作时间h={h}h__输入工况：{us}" +
               $"\n电动机功率P={P}kw__传动比i={i}___转速n={n}r/min备注：" +
               $"\n在400，700，800，950，1200，1450，1600，2000，2400，2800选" +
               $"\n计算功率Pca={Pca}kw__备注：" +
               $"\n工作情况系数Ka={Ka}__备注：表8-8P168" +
               $"\n选v带Belt={Belt}__备注：表8-11P169" +
               $"\n小带轮dD1={dD1}mm__备注：表8-7P167" +
               $"\n大带轮dD2c={dD2c}mm__dD2={dD2}mm__备注：表8-9P168" +
               $"\n验算v={v}m/s__备注：一般推荐5-25mm/s最高30m/s" +
               $"\n初定中心距a0={a0}mm__备注：公式" +
               $"\n所需基准长度Ld0={Ld0}mm__备注：无" +
               $"\n基准长度Ld={Ld}mm__备注：表8-2P157" +
               $"\n实际中心距a={a}mm__{amin}mm<a<{amax}mm" +
               $"\n验算小带轮包角α={α}°__备注：>120°" +
               $"\n基本额定功率P0={P0}kw__备注：表8-4P163" +
               $"\n基本额定功率增量ΔP0={ΔP0}kw__备注：表8-5P165" +
               $"\n包角修正系数Kα={Kα}__备注：表8-6P166" +
               $"\n修正系数KL={KL}__备注：表8-2P157" +
               $"\n额定功率Pr={Pr}__备注：" +
               $"\nv带根数z={z}根__备注：" +
               $"\n带单位长度质量q={q}kg/m__备注：表8-3P161" +
               $"\n初拉力F0={F0}N__压轴力Fp={Fp}N";
    }
    static float getKL(string Belt,float Ld)
    {
        Dictionary<string, Dictionary<float, float>> vBeltData = new Dictionary<string, Dictionary<float, float>>
                {
                    {
                        "Z", new Dictionary<float, float>
                        {
                            {405f, 0.87f}, {475f,0.9f}, {530f, 0.93f}, {625f, 0.96f}, {700f, 0.99f}, {780f, 1.00f}, {920f, 1.04f}, {1080f, 1.07f}, {1330f, 1.13f}, {1420f, 1.14f}, {1540f, 1.54f}
                        }
                    },
                    {
                        "A", new Dictionary<float, float>
                        {
                            {630f, 0.81f}, {700f, 0.83f}, {790f, 0.85f}, {890f, 0.87f}, {990f, 0.89f}, {1100f, 0.91f}, {1250f, 0.93f}, {1430f, 0.96f}, {1550f, 0.98f}, {1640f, 0.99f}, {1750f, 1.00f}, {1940f, 1.02f}, {2050f, 1.04f}, {2200f, 1.06f}, {2300f, 1.07f}, {2480f, 1.09f}, {2700f, 1.10f}
                        }
                    },
                    {
                        "B", new Dictionary<float, float>
                        {
                            {930f, 0.83f}, {1000f, 0.84f}, {1100f, 0.86f}, {1210f, 0.87f}, {1370f, 0.90f}, {1560f, 0.92f}, {1760f, 0.94f}, {1950f, 0.97f}, {2180f, 0.99f}, {2300f, 1.01f}, {2500f, 1.03f}, {2700f, 1.04f}, {2870f, 1.05f}, {3200f, 1.07f}, {3600f, 1.09f}, {4060f, 1.13f}, {4430f, 1.15f},{4820f, 1.17f},{5370f, 1.20f},{46070f, 1.24f}
                        }
                    },
                    {
                        "C", new Dictionary<float, float>
                        {
                            {1565f, 0.82f}, {1760f, 0.85f}, {1950f, 0.87f}, {2195f, 0.90f}, {2420f, 0.92f}, {2715f, 0.94f}, {2880f, 0.95f}, {3080f, 0.97f}, {3520f, 0.99f}, {4060f, 1.02f}, {4600f, 1.05f}, {5380f, 1.08f}, {6100f, 1.11f}, {6815f, 1.14f}, {7600f, 1.17f}, {9100f, 1.21f}, {10700f, 1.24f}
                        }
                    },
                    {
                        "D", new Dictionary<float, float>
                        {
                            {2740f, 0.82f}, {3100f, 0.86f}, {3330f, 0.87f}, {3730f, 0.90f}, {4080f, 0.91f}, {4620f, 0.94f}, {5400f, 0.97f}, {6100f, 0.99f}, {6840f, 1.02f}, {7620f, 1.05f}, {9140f, 1.08f}, {10700f, 1.13f}, {12200f, 1.16f}, {13700f, 1.19f}, {15200f, 1.21f}
                        }
                    },
                };
        //if(Belt==null&& Ld==0)
        //{
        //    return 0;
        //}
        //else
        //{
            float KL = vBeltData[Belt.Substring(0, 1)][Ld];
            return KL;
        //}
    }
    static float getαfitting(float α)
    {

        string Diameter = "180,174,169,163,157,151,145,139,133,127,120";
        string userMessage = $"现在我们已经求得小带轮包角是{α},已知包角有以下大小：" + Diameter + $",根据就近原则从选回答一种角度如“112”,不要回答理由";
        string response = AskDeepSeek(apiKey, userMessage);
        Console.WriteLine(response);
        string response1 = DeepSeekreason(apiKey, userMessage, response);
        Console.WriteLine(response1);
        return float.Parse(response);
    }
    //涉及推断再选择的部分就把推断全丢给deepseek
    static float getLd(string Belt,float Ld0)
    {
        if (Belt.Substring(0, 1) == "Z")
        {
            string Diameter = "405,475,530,625,700,780,920,1080,1330,1420,1540";
            string userMessage = $"现在我们已经求得所需带长是{Ld0},已知这种Z带型有以下长度：" + Diameter + $"单位(mm),根据就近原则从选回答一种长度如“112”,不要回答理由不要回答单位";
            string response = AskDeepSeek(apiKey, userMessage);
            Console.WriteLine(response);
            string response1 = DeepSeekreason(apiKey, userMessage, response);
            Console.WriteLine(response1);
            return float.Parse(response);
        }
        if (Belt.Substring(0, 1) == "A")
        {
            string Diameter = "630,700,790,890,990,1100,1250,1430,1550,1640,1750,1940,2050,2200,2300,2480,2700";
            string userMessage = $"现在我们已经求得所需带长是{Ld0},已知这种Z带型有以下长度：" + Diameter + $"单位(mm),根据就近原则从选回答一种长度如“112”,不要回答理由不要回答单位";
            string response = AskDeepSeek(apiKey, userMessage);
            Console.WriteLine(response);
            string response1 = DeepSeekreason(apiKey, userMessage, response);
            Console.WriteLine(response1);
            return float.Parse(response);
        }
        if (Belt.Substring(0, 1) == "B")
        {
            string Diameter = "930,1000,1100,1210,1370,1560,1760,1950,2180,2300,2500,2700,2870,3200,3600,4060,4430,4820,5370,6070";
            string userMessage = $"现在我们已经求得所需带长是{Ld0},已知这种Z带型有以下长度：" + Diameter + $"单位(mm),根据就近原则从选回答一种长度如“112”,不要回答理由不要回答单位";
            string response = AskDeepSeek(apiKey, userMessage);
            Console.WriteLine(response);
            string response1 = DeepSeekreason(apiKey, userMessage, response);
            Console.WriteLine(response1);
            return float.Parse(response);
        }
        if (Belt.Substring(0, 1) == "C")
        {
            string Diameter = "1565,1760,1950,2195,2420,2715,2880,3080,3520,4060,4600,5380,6100,6815,7600,9100,10700";
            string userMessage = $"现在我们已经求得所需带长是{Ld0},已知这种Z带型有以下长度：" + Diameter + $"单位(mm),根据就近原则从选回答一种长度如“112”,不要回答理由不要回答单位";
            string response = AskDeepSeek(apiKey, userMessage);
            Console.WriteLine(response);
            string response1 = DeepSeekreason(apiKey, userMessage, response);
            Console.WriteLine(response1);
            return float.Parse(response);
        }
        if (Belt.Substring(0, 1) == "D")
        {
            string Diameter = "4660,5040,5420,6100,6850,7650,9150,12230,13750,15280,16800";
            string userMessage = $"现在我们已经求得所需带长是{Ld0},已知这种Z带型有以下长度：" + Diameter + $"单位(mm),根据就近原则从选回答一种长度如“112”,不要回答理由不要回答单位";
            string response = AskDeepSeek(apiKey, userMessage);
            Console.WriteLine(response);
            string response1 = DeepSeekreason(apiKey, userMessage, response);
            Console.WriteLine(response1);
            return float.Parse(response);
        }
        else
        {
            Console.WriteLine("输入的Belt有问题");
            return float.Parse("0");
        }
    }
    //如果不喜欢deepseek也可以直接算
    static float GetLd(string belt, float ld0)
    {
        // 定义所有带型的标准长度
        var standardLengths = new Dictionary<string, float[]>
    {
        { "Z", new float[] { 405, 475, 530, 625, 700, 780, 920, 1080, 1330, 1420, 1540 } },
        { "A", new float[] { 630, 700, 790, 890, 990, 1100, 1250, 1430, 1550, 1640, 1750, 1940, 2050, 2200, 2300, 2480, 2700 } },
        { "B", new float[] { 930, 1000, 1100, 1210, 1370, 1560, 1760, 1950, 2180, 2300, 2500, 2700, 2870, 3200, 3600, 4060, 4430, 4820, 5370, 6070 } },
        { "C", new float[] { 1565, 1760, 1950, 2195, 2420, 2715, 2880, 3080, 3520, 4060, 4600, 5380, 6100, 6815, 7600, 9100, 10700 } },
        { "D", new float[] { 4660, 5040, 5420, 6100, 6850, 7650, 9150, 12230, 13750, 15280, 16800 } }
    };

        if (string.IsNullOrEmpty(belt) || !standardLengths.ContainsKey(belt.Substring(0, 1)))
        {
            Console.WriteLine("输入的Belt有问题");
            return 0f;
        }
        string beltType = belt.Substring(0, 1);
        float[] lengths = standardLengths[beltType];
        // 使用就近原则选择最接近的标准长度
        float selectedLength = FindNearestLength(ld0, lengths);
        Console.WriteLine($"选择理由：所需长度 {ld0}mm，根据就近原则选择 {selectedLength}mm");
        return selectedLength;
    }

    static float FindNearestLength(float target, float[] availableLengths)
    {
        return availableLengths.OrderBy(x => Math.Abs(x - target)).First();
    }
    static float geta0(float amin,float amax,float dD1,float dD2, string αConditon)
    {
        string feedback = null;
        if (αConditon == null)
        {
            feedback = null;
        }
        else
        {
            feedback = "之前选择的中心距太小了";
        }

        string request = $"在选择带轮的过程中，已知小带轮直径为{dD1},大带轮直径为{dD2},请帮我在{amin}-{amax}范围内选择中心距的大小"+feedback+"，直接回答数值如“400”,不要回答理由";
        string response = AskDeepSeek(apiKey, request);
        Console.WriteLine(response);
        string response1 = DeepSeekreason(apiKey, request, response);
        Console.WriteLine(response1);
        return float.Parse(response);
    }
    static string getbelt(float Pca,float n)
    {
        string 请求头 = "Z型v带：适用功率为0.8kw-4kw适用转速为600r/min-4000r/min__" +
               "A型v带：适用功率为0.8kw-8kw适用转速为160r/min-3150r/min__" +
               "B型v带：适用功率为1.6kw-16kw适用转速为100r/ming-1600r/min__" +
               "C型v带：适用功率为8kw-40kw适用转速为100r/min-800r/min__" +
               "D型v带：适用功率为22kw-100kw适用转速为100r/min-500r/min__";
        string userMessage = "已知" + 请求头 + $"根据已知功率为{Pca}已知转速为{n}选择一种带类型，直接回答类型如“M型v带”，不要回答理由";
        string response = AskDeepSeek(apiKey, userMessage);
        Console.WriteLine(response);
        string response1 = DeepSeekreason(apiKey, userMessage, response);
        Console.WriteLine(response1);
        return response;
    }
    static float getdD1(string Belt,string vCondition)
    {
        string feedback = null;
        if (vCondition==null)
        {
             feedback = null;
        }
        else if(vCondition == "small")
        {
             feedback = "上次选择的小带轮太小了，导致带速小于了最低要求5m/s";
        }
        else if( vCondition =="big")
        {
             feedback = "上次选择的小带轮太大了，导致带速大于了最低要求30m/s";
        }
        if (Belt.Substring(0, 1) == "Z")
        {
            string Diameter = "50,56,63,71,80,90";
            string userMessage = "已经选择了Z型v带，查表可知这种带适配的小带轮直径有" + Diameter + "单位(mm),请选择一种带轮直径，直接回答直径数如“104”"+ feedback+ "，不要回答理由不要回答单位";
            string response = AskDeepSeek(apiKey, userMessage);
            Console.WriteLine(response);
            string response1 = DeepSeekreason(apiKey, userMessage, response);
            Console.WriteLine(response1);
            return float.Parse(response);
        }
        if (Belt.Substring(0, 1) == "A")
        {
            string Diameter = "75,90,100,112,125,140,160,180";
            string userMessage = "已经选择了A型v带，查表可知这种带适配的小带轮直径有" + Diameter + "单位(mm),请选择一种带轮直径，直接回答直径数如“104”"+ feedback+ "，不要回答理由不要回答单位";
            string response = AskDeepSeek(apiKey, userMessage);
            Console.WriteLine(response);
            string response1 = DeepSeekreason(apiKey, userMessage, response);
            Console.WriteLine(response1);
            return float.Parse(response);
        }
        if (Belt.Substring(0, 1) == "B")
        {
            string Diameter = "125,140,160,180,200,224,250,280";
            string userMessage = "已经选择了B型v带，查表可知这种带适配的小带轮直径有" + Diameter + "单位(mm),请选择一种带轮直径，直接回答直径数如“104”"+ feedback+ "，不要回答理由不要回答单位";
            string response = AskDeepSeek(apiKey, userMessage);
            Console.WriteLine(response);
            string response1 = DeepSeekreason(apiKey, userMessage, response);
            Console.WriteLine(response1);
            return float.Parse(response);
        }
        if (Belt.Substring(0, 1) == "C")
        {
            string Diameter = "200,224,250,280,315,355,400,450";
            string userMessage = "已经选择了C型v带，查表可知这种带适配的小带轮直径有" + Diameter + "单位(mm),请选择一种带轮直径，直接回答直径数如“104”"+ feedback+ "，不要回答理由不要回答单位";
            string response = AskDeepSeek(apiKey, userMessage);
            Console.WriteLine(response);
            string response1 = DeepSeekreason(apiKey, userMessage, response);
            Console.WriteLine(response1);
            return float.Parse(response);
        }
        if (Belt.Substring(0, 1) == "D")
        {
            string Diameter = "355,400,450,500,560,630,710,800";
            string userMessage = "已经选择了D型v带，查表可知这种带适配的小带轮直径有" + Diameter + "单位(mm),请选择一种带轮直径，直接回答直径数如“104”"+ feedback+ "，不要回答理由不要回答单位";
            string response = AskDeepSeek(apiKey, userMessage);
            Console.WriteLine(response);
            string response1 = DeepSeekreason(apiKey, userMessage, response);
            Console.WriteLine(response1);
            return float.Parse(response);
        }
        else
        {
            Console.WriteLine("输入的Belt有问题");
            return float.Parse("0");
        }
    }
    static float getDd2(string Belt,float dD2c)
    {
        if (Belt.Substring(0, 1) == "Z")
        {
            string Diameter = "50,56,63,71,75,80,90,100,112,125,132,140,150,160,180,200,224,250,280,315,355,400,500,630";
            string userMessage = "已经选择了Z型v带，查表可知这种带适配的大带轮直径有" + Diameter + $"单位(mm),经过计算得到大带轮直径的精确可能值为{dD2c}请根据就近原则选择一种可适配的大带轮" +
                $"直接回答直径数如“104”，不要回答理由不要回答单位";
            string response = AskDeepSeek(apiKey, userMessage);
            Console.WriteLine(response);
            string response1 = DeepSeekreason(apiKey, userMessage, response);
            Console.WriteLine(response1);
            return float.Parse(response);
        }
        if (Belt.Substring(0, 1) == "A")
        {
            string Diameter = "75,80,85,90,95,100,106,112,118,125,132,140,150,160,180,200,224,250,280,315,355,400,450,500,560,630,710,800";
            string userMessage = "已经选择了A型v带，查表可知这种带适配的大带轮直径有" + Diameter + $"单位(mm),经过计算得到大带轮直径的精确可能值为{dD2c}请根据就近原则选择一种可适配的大带轮" +
                $"直接回答直径数如“104”，不要回答理由不要回答单位";
            string response = AskDeepSeek(apiKey, userMessage);
            Console.WriteLine(response);
            string response1 = DeepSeekreason(apiKey, userMessage, response);
            Console.WriteLine(response1);
            return float.Parse(response);
        }
        if (Belt.Substring(0, 1) == "B")
        {
            string Diameter = "125,132,140,150,160,170,180,200,224,250,280,315,355,400,450,500,560,600,630,710,750,800,900,1000,1120";
            string userMessage = "已经选择了Z型v带，查表可知这种带适配的大带轮直径有" + Diameter + $"单位(mm),经过计算得到大带轮直径的精确可能值为{dD2c}请根据就近原则选择一种可适配的大带轮" +
                $"直接回答直径数如“104”，不要回答理由不要回答单位";
            string response = AskDeepSeek(apiKey, userMessage);
            Console.WriteLine(response);
            string response1 = DeepSeekreason(apiKey, userMessage, response);
            Console.WriteLine(response1);
            return float.Parse(response);
        }
        if (Belt.Substring(0, 1) == "C")
        {
            string Diameter = "200,212,224,236,250,265,280,300,315,335,355,400,450,500,560,600,630,710,750,800,900,1000,1120,1250,1400,1600,2000";
            string userMessage = "已经选择了Z型v带，查表可知这种带适配的大带轮直径有" + Diameter + $"单位(mm),经过计算得到大带轮直径的精确可能值为{dD2c}请根据就近原则选择一种可适配的大带轮" +
                $"直接回答直径数如“104”，不要回答理由不要回答单位";
            string response = AskDeepSeek(apiKey, userMessage);
            Console.WriteLine(response);
            string response1 = DeepSeekreason(apiKey, userMessage, response);
            Console.WriteLine(response1);
            return float.Parse(response);
        }
        if (Belt.Substring(0, 1) == "D")
        {
            string Diameter = "355,375,400,425,450,475,500,560,600,630,710,750,800,900,1000,1120,1250,1400,1500,1600,1800,2000";
            string userMessage = "已经选择了Z型v带，查表可知这种带适配的大带轮直径有" + Diameter + $"单位(mm),经过计算得到大带轮直径的精确可能值为{dD2c}请根据就近原则选择一种可适配的大带轮" +
                $"直接回答直径数如“104”，不要回答理由不要回答单位";
            string response = AskDeepSeek(apiKey, userMessage);
            Console.WriteLine(response);
            string response1 = DeepSeekreason(apiKey, userMessage, response);
            Console.WriteLine(response1);
            return float.Parse(response);
        }
        else
        {
            Console.WriteLine("输入的Belt有问题");
            return float.Parse("0");
        }
    }
    static string DeepSeekreason(string apiKey,string userMessage,string response1)
    {
        var client = new RestClient("https://api.deepseek.com/chat/completions");
        var request = new RestRequest();
        request.Method = Method.Post;
        string userMessage1 = "已经提问:“" + userMessage + "”已经回答:“" + response1 + "”根据上面的选择回答这么选择的理由只用一段话（200字）（请务必回答理由而不是回答提问）";
        // 设置请求头
        request.AddHeader("Authorization", $"Bearer {apiKey}");
        request.AddHeader("Content-Type", "application/json");

        // 构建请求体，使用传入的用户消息
        string requestBody = $@"{{
        ""messages"": [
            {{
                ""role"": ""user"",
                ""content"": ""{userMessage1}""
            }}
        ],
        ""model"": ""deepseek-chat"",
        ""max_tokens"": 500,
        ""stream"": false,
        ""top_p"": 0.9,
        ""temperature"": 0.3,
        ""frequency_penalty"": 0.1,
        ""presence_penalty"": 0.1
        }}";
        request.AddParameter("application/json", requestBody, ParameterType.RequestBody);
        // 发送请求
        Console.WriteLine("正在让ds回答原因");
        var response = client.Execute(request);

        if (response.IsSuccessful)
        {
            // 解析JSON响应，提取纯文本内容
            try
            {
                var jsonResponse = JsonSerializer.Deserialize<JsonElement>(response.Content);
                if (jsonResponse.TryGetProperty("choices", out JsonElement choices) &&
                    choices.GetArrayLength() > 0)
                {
                    var firstChoice = choices[0];
                    if (firstChoice.TryGetProperty("message", out JsonElement message) &&
                        message.TryGetProperty("content", out JsonElement content))
                    {
                        return content.GetString()?.Trim() ?? "API返回内容为空";
                    }
                }
                return "无法解析API响应中的内容";
            }
            catch (Exception ex)
            {
                return $"解析响应时出错: {ex.Message}";
            }
        }
        else
        {
            return $"API请求失败: {response.StatusCode} - {response.ErrorMessage}";
        }
    }
    static string AskDeepSeek(string apiKey, string userMessage)
    {
        // 设置客户端
        var client = new RestClient("https://api.deepseek.com/chat/completions");
        var request = new RestRequest();
        request.Method = Method.Post;

        // 设置请求头
        request.AddHeader("Authorization", $"Bearer {apiKey}");
        request.AddHeader("Content-Type", "application/json");

        // 构建请求体，使用传入的用户消息
        string requestBody = $@"{{
        ""messages"": [
            {{
                ""role"": ""user"",
                ""content"": ""{userMessage}""
            }}
        ],
        ""model"": ""deepseek-chat"",
        ""max_tokens"": 100,
        ""stream"": false,
        ""top_p"": 0.9,
        ""temperature"": 0.3,
        ""frequency_penalty"": 0.1,
        ""presence_penalty"": 0.1
        }}";
        request.AddParameter("application/json", requestBody, ParameterType.RequestBody);
        // 发送请求
        Console.WriteLine("正在让ds选择");
        var response = client.Execute(request);

        if (response.IsSuccessful)
        {
            // 解析JSON响应，提取纯文本内容
            try
            {
                var jsonResponse = JsonSerializer.Deserialize<JsonElement>(response.Content);
                if (jsonResponse.TryGetProperty("choices", out JsonElement choices) &&
                    choices.GetArrayLength() > 0)
                {
                    var firstChoice = choices[0];
                    if (firstChoice.TryGetProperty("message", out JsonElement message) &&
                        message.TryGetProperty("content", out JsonElement content))
                    {
                        return content.GetString()?.Trim() ?? "API返回内容为空";
                    }
                }
                return "无法解析API响应中的内容";
            }
            catch (Exception ex)
            {
                return $"解析响应时出错: {ex.Message}";
            }
        }
        else
        {
            return $"API请求失败: {response.StatusCode} - {response.ErrorMessage}";
        }
    }

    public static string[] input()
    {
        // 提示用户输入命令
        Console.Write("请输入命令: ");

        // 读取一行命令行输入
        string input = Console.ReadLine();

        // 检查输入是否为空
        if (string.IsNullOrWhiteSpace(input))
        {
            return Array.Empty<string>();
        }

        // 使用空格分割输入，并过滤掉可能的空项
        // StringSplitOptions.RemoveEmptyEntries 确保不会包含空字符串
        string[] commandParts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        return commandParts;
    }
}
