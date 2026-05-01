using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// AES-256-CBC titkosított mentési rendszer + HMAC-SHA256 integritás ellenőrzés.
///
/// Hogyan védi az adatokat:
///   - A kulcs az eszköz egyedi azonosítójából + egy hardcoded salt-ból képzett SHA-256 hash.
///     Más eszközön nem olvasható fel (eszközkötött mentés).
///   - Az IV minden mentésnél véletlenszerű → replay-védelem.
///   - A HMAC-SHA256 aláírás detektálja a kézi módosítást.
///
/// Fájlok helye: Application.persistentDataPath
///   {key}.dat  – titkosított mentés (IV || ciphertext)
///   {key}.sig  – HMAC aláírás
///
/// Kulcsok:
///   "roster"       → fiók adatok + karakterlista
///   "char_{id}"    → egy karakter adatai
/// </summary>
public static class EncryptedDataStore
{
    private const string SALT = "TD_GAME_SALT_2025_v1";

    // ── Kulcsgenerálás ──────────────────────────────────────────────────

    private static byte[] GetKey()
    {
        string raw = SystemInfo.deviceUniqueIdentifier + SALT;
        using var sha = SHA256.Create();
        return sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
    }

    // ── Elérési utak ────────────────────────────────────────────────────

    private static string DataPath(string key) =>
        Path.Combine(Application.persistentDataPath, key + ".dat");

    private static string SigPath(string key) =>
        Path.Combine(Application.persistentDataPath, key + ".sig");

    // ── Mentés ──────────────────────────────────────────────────────────

    public static void Save(string key, string json)
    {
        try
        {
            byte[] encKey     = GetKey();
            byte[] plaintext  = Encoding.UTF8.GetBytes(json);

            using (var hmac = new HMACSHA256(encKey))
                File.WriteAllBytes(SigPath(key), hmac.ComputeHash(plaintext));

            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode    = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key     = encKey;
            aes.GenerateIV();

            using var ms = new MemoryStream();
            ms.Write(aes.IV, 0, aes.IV.Length);
            using (var encryptor = aes.CreateEncryptor())
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write, leaveOpen: true))
            {
                cs.Write(plaintext, 0, plaintext.Length);
                cs.FlushFinalBlock();
            }
            File.WriteAllBytes(DataPath(key), ms.ToArray());
        }
        catch (Exception e)
        {
            Debug.LogError($"EncryptedDataStore.Save({key}) hiba: {e.Message}");
        }
    }

    // ── Betöltés ────────────────────────────────────────────────────────

    public static string Load(string key)
    {
        string datPath = DataPath(key);
        if (!File.Exists(datPath)) return null;

        try
        {
            byte[] encKey     = GetKey();
            byte[] cipherdata = File.ReadAllBytes(datPath);

            if (cipherdata.Length < 17)
            {
                Delete(key);
                return null;
            }

            byte[] iv         = new byte[16];
            byte[] ciphertext = new byte[cipherdata.Length - 16];
            Array.Copy(cipherdata, 0, iv, 0, 16);
            Array.Copy(cipherdata, 16, ciphertext, 0, ciphertext.Length);

            byte[] plaintext;
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode    = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key     = encKey;
                aes.IV      = iv;
                using var ms = new MemoryStream(ciphertext);
                using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
                using var result = new MemoryStream();
                cs.CopyTo(result);
                plaintext = result.ToArray();
            }

            string sigPath = SigPath(key);
            if (File.Exists(sigPath))
            {
                byte[] storedSig = File.ReadAllBytes(sigPath);
                byte[] calcSig;
                using (var hmac = new HMACSHA256(encKey))
                    calcSig = hmac.ComputeHash(plaintext);

                if (!ConstantTimeEquals(calcSig, storedSig))
                {
                    Debug.LogWarning($"EncryptedDataStore: HMAC eltérés ({key}) – mentés módosítva! Adatok visszautasítva.");
                    Delete(key);
                    return null;
                }
            }

            return Encoding.UTF8.GetString(plaintext);
        }
        catch (Exception e)
        {
            Debug.LogError($"EncryptedDataStore.Load({key}) hiba: {e.Message}");
            return null;
        }
    }

    // ── Törlés ──────────────────────────────────────────────────────────

    public static void Delete(string key)
    {
        try
        {
            if (File.Exists(DataPath(key))) File.Delete(DataPath(key));
            if (File.Exists(SigPath(key)))  File.Delete(SigPath(key));
        }
        catch (Exception e)
        {
            Debug.LogError($"EncryptedDataStore.Delete({key}) hiba: {e.Message}");
        }
    }

    public static bool Exists(string key) => File.Exists(DataPath(key));

    // ── Segédmetódusok ──────────────────────────────────────────────────

    private static bool ConstantTimeEquals(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++)
            diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
