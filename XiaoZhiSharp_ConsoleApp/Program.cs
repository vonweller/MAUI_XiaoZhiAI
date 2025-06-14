﻿using Newtonsoft.Json;
using System.Net.NetworkInformation;
using XiaoZhiSharp;
using XiaoZhiSharp.Protocols;
using XiaoZhiSharp.Services;

class Program
{
    private static XiaoZhiAgent? _xiaoZhiAgent;
    private static bool _status = false;
    static async Task Main(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Title = "小智AI 控制台客户端";
        // 定义默认值
        string OTA_VERSION_URL = "https://api.tenclass.net/xiaozhi/ota/";
        string WEB_SOCKET_URL = "wss://api.tenclass.net/xiaozhi/v1/";
        //string WEB_SOCKET_URL = "ws://192.168.10.29:8000";
        string MAC_ADDR = "";
        string logoAndCopyright = @"
========================================================================
欢迎使用“小智AI 控制台客户端” ！版本 v1.0.1
当前功能：
1. 语音消息 输入回车：开始录音；再次输入回车：结束录音
2. 文字消息 可以随意输入文字对话
3. 全量往返协议输出，方便调试      
要是你在使用中有啥想法或者遇到问题，别犹豫，找我们哟：
微信：Vonweller       电子邮箱：529538187@qq.com
========================================================================";
        Console.WriteLine(logoAndCopyright);
        Console.WriteLine("启动：XinZhiSharp_Test.exe <OTA_VERSION_URL> <WEB_SOCKET_URL> <MAC_ADDR>");
        Console.WriteLine("默认OTA_VERSION_URL：" + OTA_VERSION_URL);
        Console.WriteLine("默认WEB_SOCKET_URL：" + WEB_SOCKET_URL);
        Console.WriteLine("========================================================================");

        // 检查是否有传入参数，如果有则覆盖默认值
        if (args.Length >= 1)
        {
            OTA_VERSION_URL = args[0];
        }
        if (args.Length >= 2)
        {
            WEB_SOCKET_URL = args[1];
        }
        if (args.Length >= 3)
        {
            MAC_ADDR = args[2];
        }
        _xiaoZhiAgent = new XiaoZhiAgent(OTA_VERSION_URL, WEB_SOCKET_URL, MAC_ADDR);
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("当前 OTA_VERSION_URL：" + _xiaoZhiAgent.OTA_VERSION_URL);
        Console.WriteLine("当前 WEB_SOCKET_URL：" + _xiaoZhiAgent.WEB_SOCKET_URL);
        Console.WriteLine("当前 MAC_ADDR：" + _xiaoZhiAgent.MAC_ADDR);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("========================================================================");
        _xiaoZhiAgent.IsLogWrite = true;
        _xiaoZhiAgent.Start();
        _xiaoZhiAgent.OnMessageEvent += _xiaoZhiAgent_OnMessageEvent;
        await Task.Delay(1000);



        // 1. 注册生成设备描述
       // var descriptor = new IoTDescriptor();
      //  descriptor.AddDevice(new Lamp());
     //   descriptor.AddDevice(new DuoJI());
     //  descriptor.AddDevice(new Camre());
      //  string descriptorJson = JsonConvert.SerializeObject(descriptor, Formatting.Indented);
      //  await Task.Delay(1000);
       // await _xiaoZhiAgent.IotInit(descriptorJson);
        //await _xiaoZhiAgent.Send_Listen_Detect("你好啊,当前虚拟设备有啥");


        while (true)
        {
            string? input = Console.ReadLine();
            if (string.IsNullOrEmpty(input))
            {
                if (_status == false)
                {
                    _status = true;
                    await _xiaoZhiAgent.Send_Listen_Start("manual");
                    Console.Title = "小智AI 开始录音...";
                    Console.WriteLine("开始录音... 再次回车结束录音");
                    continue;
                }
                else
                {
                    if (_status == true)
                    {
                        _status = false;
                        await _xiaoZhiAgent.Send_Listen_Stop();
                        Console.Title = "小智AI 控制台客户端";
                        Console.WriteLine("结束录音");
                        continue;
                    }
                }
                //Console.WriteLine("空格");
                continue;
            }
            else
            {
                if (_status == false)
                {
                    if (input == "restart")
                    {
                        _xiaoZhiAgent.Restart();
                        continue;
                    }
                    await _xiaoZhiAgent.Send_Listen_Detect(input);
                }
            }
        }
    }

    private static void _xiaoZhiAgent_OnMessageEvent(string message)
    {
        dynamic? msg = JsonConvert.DeserializeObject<dynamic>(message);
        if (msg != null)
        {
            if (msg.type == "tts") {
                if (msg.state == "sentence_start") {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"小智：{msg.text}");
                    Console.ForegroundColor = ConsoleColor.Blue;
                }
            }

            if (msg.type == "stt") {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"{msg.text}");
            }

            if (msg.type=="iot")
            {
                Console.WriteLine(msg);
               var handler = new IoTCommandHandler(new Lamp(), new DuoJI(), new Camre());
               var data= handler.HandleCommand(message);
                if (data.Success)
                {
                    Task.Run(async () => await _xiaoZhiAgent.IotState(data.StateJson));                    
                }
            }
            
        }
    }
}