using ACL.Core.Domain;

namespace ACL.Testing;

/// <summary>测试数据辅助。</summary>
public static class TestData
{
    public static byte[] MakeBytes(int length, int seed = 0)
    {
        byte[] data = new byte[length];
        for (int i = 0; i < length; i++)
        {
            data[i] = (byte)((i * 31 + seed) % 256);
        }

        return data;
    }

    public static HashSpec Sha1Of(byte[] data)
        => HashSpec.Sha1(Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(data)).ToLowerInvariant());
}