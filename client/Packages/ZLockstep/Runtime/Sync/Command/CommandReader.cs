using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ZLockstep.Sync.Command;
using Utils;

namespace Utils
{
    public class CommandReader
    {
        private List<FrameInput> _frameInputs;
        
        public List<FrameInput> FrameInputs 
        { 
            get { return _frameInputs ?? (_frameInputs = new List<FrameInput>()); } 
        }

        /// <summary>
        /// 从文件读取命令记录
        /// </summary>
        /// <param name="filePath">记录文件路径</param>
        public void LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                zUDebug.LogWarning($"[CommandReader] 文件不存在: {filePath}");
                _frameInputs = new List<FrameInput>();
                return;
            }

            try
            {
                _frameInputs = new List<FrameInput>();
                
                string[] lines = File.ReadAllLines(filePath);
                
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                        
                    try
                    {
                        // 解析JSON字符串
                        JObject jsonObject = JObject.Parse(line);
                        
                        // 检查是否是frameInput类型
                        if (jsonObject["type"]?.ToString() == "frameInput")
                        {
                            // 反序列化为FrameInput对象
                            FrameInput frameInput = new FrameInput
                            {
                                type = jsonObject["type"]?.ToString(),
                                frame = int.Parse(jsonObject["frame"]?.ToString()),
                                data = new List<MyCommand>()
                            };
                            
                            // 处理命令数据，将JSON字符串转换为具体的命令对象
                            foreach (var myCommand in jsonObject["data"])
                            {
                                int commandType = myCommand["commandType"]?.ToObject<int>() ?? 0;
                                if (commandType != 0 && !string.IsNullOrEmpty(myCommand["command"].ToString()))
                                {
                                    // 使用FrameInputProcessor反序列化命令
                                    ICommand command = FrameInputProcessor.DeserializeCommand(commandType, myCommand["command"].ToString());
                                    if (command != null)
                                    {
                                        frameInput.data.Add(new MyCommand
                                        {
                                            commandType = commandType,
                                            command = command
                                        });
                                    } else
                                    {
                                        zUDebug.LogWarning($"[CommandReader] 命令反序列化失败: {myCommand}");
                                    }
                                } else
                                {
                                    zUDebug.LogWarning($"[CommandReader] 命令数据无效: {myCommand}");
                                }
                            }
                            
                            _frameInputs.Add(frameInput);
                        }
                    }
                    catch (JsonException ex)
                    {
                        zUDebug.LogError($"[CommandReader] 解析JSON行时出错: {ex.Message}, 内容: {line}");
                    }
                }
                
                zUDebug.Log($"[CommandReader] 成功加载 {_frameInputs.Count} 条frameInput记录");
            }
            catch (Exception ex)
            {
                zUDebug.LogError($"[CommandReader] 读取文件时发生错误: {ex.Message}");
                _frameInputs = new List<FrameInput>();
            }
        }

        /// <summary>
        /// 获取指定帧的命令
        /// </summary>
        /// <param name="frame">帧号</param>
        /// <returns>指定帧的命令列表</returns>
        public List<MyCommand> GetCommandsByFrame(int frame)
        {
            if (_frameInputs == null)
                return new List<MyCommand>();

            var commands = new List<MyCommand>();
            
            foreach (var frameInput in _frameInputs)
            {
                if (frameInput.frame == frame && frameInput.data != null)
                {
                    commands.AddRange(frameInput.data);
                }
            }
            
            return commands;
        }

        /// <summary>
        /// 获取所有不同的帧号
        /// </summary>
        /// <returns>帧号列表</returns>
        public List<int> GetAllFrames()
        {
            if (_frameInputs == null)
                return new List<int>();

            var frames = new HashSet<int>();
            
            foreach (var frameInput in _frameInputs)
            {
                frames.Add(frameInput.frame);
            }
            
            var sortedFrames = new List<int>(frames);
            sortedFrames.Sort();
            
            return sortedFrames;
        }
    }
}