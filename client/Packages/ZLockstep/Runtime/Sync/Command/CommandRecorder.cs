using System;
using System.IO;
using ZLockstep.Sync.Command;

namespace Utils
{
    public class CommandRecorder
    {
        private string _filePath;
        private bool _isRecording;

        /// <summary>
        /// 开始序列化记录
        /// </summary>
        /// <param name="filePath">序列化文件路径名称</param>
        public void StartRecording(string filePath)
        {
            _filePath = filePath;
            _isRecording = true;
            
            // 清空文件内容，准备新的记录
            File.WriteAllText(_filePath, string.Empty);
            zUDebug.Log($"[CommandRecorder] 开始记录命令，文件路径: {_filePath}");
        }

        /// <summary>
        /// 记录command命令到文件末尾
        /// </summary>
        /// <param name="frame">帧号</param>
        /// <param name="command">命令对象</param>
        public void RecordCommand(int frame, ICommand command)
        {
            if (!_isRecording || string.IsNullOrEmpty(_filePath))
            {
                zUDebug.LogWarning("[CommandRecorder] 录制未开始，请先调用StartRecording方法");
                return;
            }

            try
            {
                // 使用FrameInputProcessor序列化frameInput
                string serializedData = FrameInputProcessor.SerializeFrameInput(frame, command);
                
                // 将序列化数据追加到文件末尾
                File.AppendAllText(_filePath, serializedData + Environment.NewLine);
            }
            catch (Exception ex)
            {
                zUDebug.LogError($"[CommandRecorder] 记录命令时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止记录
        /// </summary>
        public void StopRecording()
        {
            if (_isRecording)
            {
                _isRecording = false;
                zUDebug.Log("[CommandRecorder] 停止记录命令");
            }
        }

        /// <summary>
        /// 检查是否正在录制
        /// </summary>
        public bool IsRecording => _isRecording;
    }
}