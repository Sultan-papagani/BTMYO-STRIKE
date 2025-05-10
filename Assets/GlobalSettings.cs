using UnityEngine;

public class GlobalSettings : MonoBehaviour
{
    public static GlobalSettings singleton;

    public string Name = "hata";
    public int Takim = 0;

    void Start()
    {
        singleton = this;
    }
}
