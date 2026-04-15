using System;

namespace AgeLanServer.Common.ServerCommunication
{
    /// <summary>
    /// Các hằng số loại thông điệp giao tiếp server
    /// </summary>
    public static class MessageTypes
    {
        public const string MessageRequest = "request";
        public const string MessageWss = "wss";
    }

    /// <summary>
    /// Cấu trúc chứa loại thông điệp
    /// </summary>
    public struct MessageType
    {
        public string Type { get; set; }
    }

    /// <summary>
    /// Cấu trúc chứa thời gian hoạt động (uptime)
    /// </summary>
    public struct Uptime
    {
        public TimeSpan UptimeDuration { get; set; }
    }

    /// <summary>
    /// Cấu trúc chứa thông tin người gửi
    /// </summary>
    public struct Sender
    {
        public string SenderName { get; set; }
    }

    /// <summary>
    /// Cấu trúc chứa nội dung body
    /// </summary>
    public struct Body
    {
        public byte[] BodyData { get; set; }
    }

    /// <summary>
    /// Cấu trúc chứa hash của body (SHA-512, 64 bytes)
    /// </summary>
    public struct BodyHash
    {
        public byte[] BodyHashData { get; set; } // Mảng 64 byte chứa hash SHA-512

        public BodyHash(byte[] hash)
        {
            if (hash == null || hash.Length != 64)
            {
                throw new ArgumentException("Body hash must be exactly 64 bytes (SHA-512)");
            }
            BodyHashData = hash;
        }
    }
}
