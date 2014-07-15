using System;
using System.Security.Cryptography;
using System.Text;

namespace BusStorm.SimpleMessage
{
  internal class BusCryptor
  {
    private readonly SymmetricAlgorithm _algorithm = new RijndaelManaged();

    public byte[] EncryptData(byte[] data, string password)
    {
      GetKey(password);
      var encryptor = _algorithm.CreateEncryptor();
      var cryptoData = encryptor.TransformFinalBlock(data, 0, data.Length);
      return cryptoData;
    }

    public byte[] DecryptData(byte[] cryptoData, string password)
    {
      GetKey(password);
      var decryptor = _algorithm.CreateDecryptor();
      var data = decryptor.TransformFinalBlock(cryptoData, 0, cryptoData.Length);
      return data;
    }

    private void GetKey(string password)
    {
      var salt = new byte[8];
      var passwordBytes = Encoding.ASCII.GetBytes(password);
      var length = Math.Min(passwordBytes.Length, salt.Length);
      for (var i = 0; i < length; i++)
      {
        salt[i] = passwordBytes[i];
      }

      var key = new Rfc2898DeriveBytes(password, salt);
      _algorithm.Key = key.GetBytes(_algorithm.KeySize / 8);
      _algorithm.IV = key.GetBytes(_algorithm.BlockSize / 8);
    }
  }
}