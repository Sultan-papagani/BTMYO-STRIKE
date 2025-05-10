using FishNet;
using FishNet.Discovery;
using FishNet.Managing;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Windows;
using static UnityEngine.Rendering.DebugUI;

public class CanvasManager : MonoBehaviour
{
    public Transform oyunPanel;
    public TextMeshProUGUI oldun_yazi;
    public GameObject HasarImlecObjesi;
    public static CanvasManager singleton;
    public Slider canbar;

    public GameObject AyarlarPanel;
    public bool gamerunning = false;

    public float indicatorSpawnTime = 0.3f;
    float time;

    public Oyuncu oyuncu = null;
    public TMP_Dropdown takim_dropdown;
    public Slider sens_slider;

    public GameObject oyunOlustur, oyunGir, anaMenu;

    public TMP_InputField isim;
    public TMP_InputField isim_serverbul;
    public TMP_InputField port;

    private NetworkManager _networkManager;

    List<string> ipAdresleri = new List<string>();
    [SerializeField] NetworkDiscovery discovery;
    [SerializeField] GameObject buton;
    [SerializeField] Transform sunucuListe;


    public void Start()
    {
        singleton = this;
        AyarlarKapa(); // açýk býrakýrsam falan.
        oyunPanel.gameObject.SetActive(false);
        oyunOlustur.SetActive(false);
        oyunGir.SetActive(false);
        anaMenu.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        _networkManager = FindFirstObjectByType<NetworkManager>();
        discovery.ServerFoundCallback += SunucuBulundu;
    }

    public void oyunGirMenuAc(){ oyunGir.SetActive(true); }
    public void oyunGirMenuKapa() { oyunGir.SetActive(false); }
    public void oyunOlusturMenuAc() { oyunOlustur.SetActive(true); }
    public void oyunOlusturMenuKapa() { oyunOlustur.SetActive(false); }
    public void anaMenuAc() { anaMenu.SetActive(true); }
    public void anaMenuKapa() { anaMenu.SetActive(false); }

    public void OyunHostla()
    {
        if (isim.text.Trim() != "")
        {
            // start client and server.
            GlobalSettings.singleton.Name = isim.text;
            _networkManager.ServerManager.StartConnection();
            _networkManager.ClientManager.StartConnection();
        }
    }

    private void Update()
    {

        if (UnityEngine.Input.GetKeyDown(KeyCode.Escape) && gamerunning)
        {
            AyarlarToggle();
        }

        time += Time.deltaTime;
    }

    public void SetCanBar(int value, Vector3 atakyonu, Transform bizpos)
    {

        float deger = value / 100f;
        if (value <= 0) {
            canbar.value = 0;
        }
        else
        {
            canbar.value = deger;
        }

        if (time > indicatorSpawnTime)
        {
            HasarImleci(atakyonu, bizpos);
            time = 0;
        }
    }

    public void SetCanBar(int value)
    {
        // hayata döndük
        if (value == 100)
        {
            oldun_yazi.text = "";
            canbar.value = value;
        }
    }

    public void HasarImleci(Vector3 atakyonu, Transform bizpos)
    {
        Instantiate(HasarImlecObjesi, oyunPanel).GetComponent<HasarImleci>().Init(atakyonu, 2f, bizpos);
    }

    public void SetOlumEkrani(string dusman_adi)
    {
        oldun_yazi.text = "<color=red>" + dusman_adi + "</color> TARAFINDAN VURULDUN!";
    }


    public void AyarlarToggle()
    {
        AyarlarPanel.SetActive(!AyarlarPanel.activeSelf);
        Cursor.lockState = !AyarlarPanel.activeSelf ? CursorLockMode.Locked : CursorLockMode.None;
    }

    public void AyarlarAc()
    {
        AyarlarPanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;

    }

    public void AyarlarKapa()
    {
        AyarlarPanel.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void OnChangeSensvity(float value)
    {
        if (oyuncu != null) {
            oyuncu.sensX = value;
            oyuncu.sensY = value;
        }
    }

    public void setupSettingsDefault()
    {
        takim_dropdown.value = GlobalSettings.singleton.Takim;
        if (oyuncu != null)
        { 
            sens_slider.value = oyuncu.sensX;
            oyunPanel.gameObject.SetActive(true);

            AyarlarKapa();
            oyunOlustur.SetActive(false);
            oyunGir.SetActive(false);
            anaMenu.SetActive(false);
        }
    }

    public void OnChangeTakim(int takim)
    {
        if (oyuncu != null)
        {
            oyuncu.takimDegis(takim);
        }
    }

    void SunucuBulundu(IPEndPoint endpoint)
    {
        foreach (string adres in ipAdresleri)
        {
            if (adres == endpoint.Address.ToString()) { return; }
        }

        ipAdresleri.Add(endpoint.Address.ToString());
        GameObject x = Instantiate(buton);
        x.transform.SetParent(sunucuListe);
        x.transform.localScale = new Vector3(1, 1, 1);
        SunucuButon y = x.GetComponent<SunucuButon>();
        y.sunucu_Adi.text = endpoint.Address.ToString();
        var temp = endpoint;
        y.katil_buton.onClick.AddListener(() => SunucuKatil(temp));
    }

    void SunucuKatil(IPEndPoint a)
    {
        if (isim_serverbul.text.Trim() == "") { return; }
        GlobalSettings.singleton.Name = isim_serverbul.text;
        discovery.StopSearchingOrAdvertising();
        string newip = a.Address.ToString();
        InstanceFinder.ClientManager.StartConnection(newip);
    }

    public void OnClickRefresh()
    {
        discovery.SearchForServers();
    }
}
