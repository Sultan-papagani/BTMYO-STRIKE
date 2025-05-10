using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Managing.Logging;
using UnityEngine.InputSystem;
using FishNet.Object.Prediction;
using FishNet.Object.Synchronizing;
using Unity.VisualScripting;
using System.Collections;
using FishNet;
using FishNet.Component.Spawning;
using System.Collections.Generic;

public class Oyuncu : NetworkBehaviour
{
    public float hareketHizi;
    public float sensX;
    public float sensY;
    public float jumpForce =5f;
    public float playerHeight = 2f;
    public float groundDrag = 6f;
    public float airDrag = 2f;
    public float multiplier = 0.01f;
    public float movementmultiplier = 10f;
    public float airmultiplier = 0.4f;
    Rigidbody rb;
    public Transform cameraHolder;
    public GameObject skin;
    Camera cam;
    float mouseX;
    float mouseY;
    public float xRotation;
    public float yRotation;
    bool isGrounded;
    float horizontal;
    float vertical;
    Vector3 moveDir;
    Shoot shoot;

    int layerMask;

    bool kontroller = true;

    private readonly SyncVar<int> Can = new SyncVar<int>(new SyncTypeSettings(WritePermission.ClientUnsynchronized));
    [ServerRpc(RunLocally = true)] private void SetCan(int can) => Can.Value = can;


    public readonly SyncVar<string> Name = new SyncVar<string>(new SyncTypeSettings(WritePermission.ClientUnsynchronized));
    [ServerRpc(RunLocally = true)] private void SetName(string value) => Name.Value = value;

    //public int TAKIM = 0; // 0 = a, 1 = b olsun

    public readonly SyncVar<int> Takim = new SyncVar<int>(new SyncTypeSettings(WritePermission.ClientUnsynchronized));
    [ServerRpc(RunLocally = true)] private void SetTakim(int can) => Takim.Value = can;

    public enum VurusTipi
    {
        hayatta,
        oldu
    }

    public override void OnStartClient()
    {
        layerMask = LayerMask.GetMask("player");
        shoot = GetComponent<Shoot>();

        if (base.IsOwner)
        {
            cam = Camera.main;
            rb = GetComponent<Rigidbody>();

            SetName(GlobalSettings.singleton.Name);
            SetTakim(GlobalSettings.singleton.Takim);
            SetCan(100);

            CanvasManager.singleton.gamerunning = true;
            CanvasManager.singleton.oyuncu = this;
            CanvasManager.singleton.setupSettingsDefault();

            cam.transform.SetParent(cameraHolder);
            cam.transform.localPosition = Vector3.zero;
            rb.freezeRotation = true;
            Cursor.lockState = CursorLockMode.Locked;
            skin.SetActive(false);
        }
        else
        {
            GetComponent<Rigidbody>().isKinematic = true;
        }

    }

    void Start()
    {
        
    }

    [ServerRpc]
    public void server_shoot(int hasar)
    {
        // raycast
        Debug.DrawRay(cameraHolder.position, cameraHolder.forward * 100f, Color.red, 100f);
        if (Physics.Raycast(cameraHolder.position, cameraHolder.forward, out RaycastHit hit, 100f)){


            // adamýn caný indir, 0 ve aþaðý inerse öldür
            if (hit.transform.TryGetComponent<Oyuncu>(out Oyuncu oyuncu)){
                if (!(base.Owner.ClientId == oyuncu.Owner.ClientId))
                {
                    if (oyuncu.Can.Value < 0)
                    {
                        // do nothin.
                        return;
                    }
                    oyuncu.Can.Value -= hasar;
                    oyuncu.HasarAl(oyuncu.Owner, (oyuncu.Can.Value <= 0) ? VurusTipi.oldu : VurusTipi.hayatta, transform.position, Name.Value);
                    if (oyuncu.Can.Value <= 0)
                    {
                        oyuncu.Can.Value = 100;
                        oyuncu.Death();
                        StartCoroutine(Canlandir(oyuncu));
                    }
                }
            }
        }
    }

    private IEnumerator Canlandir(Oyuncu oyuncu)
    {
        yield return new WaitForSeconds(5f);
        int spawnpos = Random.Range(0, PlayerSpawnerNew.singleton.takim_A.Count);
        oyuncu.transform.position = (Takim.Value == 0 ? PlayerSpawnerNew.singleton.takim_A[spawnpos] : PlayerSpawnerNew.singleton.takim_B[spawnpos]).position;
        oyuncu.ReturnToLife(spawnpos);
    }

    [TargetRpc]
    public void HasarAl(NetworkConnection conn, VurusTipi tip, Vector3 vuran, string vuran_kisi)
    {
        if (tip == VurusTipi.oldu) 
        {
            CanvasManager.singleton.SetOlumEkrani(vuran_kisi);
            kontroller = false;
        }
        else
        {
            yRotation -= Random.Range(0.4f, 0.7f);
        }

        CanvasManager.singleton.SetCanBar(Can.Value, vuran, transform);

    }

    public void takimDegis(int takim)
    {
        SetTakim(takim);
    }

    [ObserversRpc]
    public void Death()
    {
        shoot.GunVisibility(false);
        skin.SetActive(false);
        GetComponent<CapsuleCollider>().enabled = false;
        GetComponent<Rigidbody>().useGravity = false;
    }

    [ObserversRpc]
    public void ReturnToLife( int pos)
    {
        transform.position = (Takim.Value == 0 ? PlayerSpawnerNew.singleton.takim_A[pos] : PlayerSpawnerNew.singleton.takim_B[pos]).position;
        shoot.GunVisibility(true);
        GetComponent<CapsuleCollider>().enabled = true;
        GetComponent<Rigidbody>().useGravity = true;

        if (!base.IsOwner) { 
            skin.SetActive(true);
        }

        if (base.IsOwner) {
            kontroller = true;
            CanvasManager.singleton.SetCanBar(100);
        }
    }

    void Update()
    {
        if (!base.IsOwner){return;}
        if (!kontroller) { return;}

        isGrounded = Physics.Raycast(transform.position, Vector3.down, playerHeight / 2 + 0.1f);
        rb.linearDamping = isGrounded ? groundDrag : airDrag;
        HandleInput();

        cameraHolder.localRotation = Quaternion.Euler(yRotation, 0, 0);
        transform.rotation = Quaternion.Euler(0, xRotation, 0);
    }

    void HandleInput()
    {
        horizontal = Input.GetAxisRaw("Horizontal");
        vertical = Input.GetAxisRaw("Vertical");
        moveDir = transform.forward * vertical + transform.right * horizontal;

        mouseX = Input.GetAxisRaw("Mouse X");
        mouseY = Input.GetAxisRaw("Mouse Y");

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded){
            Jump();
        }

        xRotation += mouseX * sensX * multiplier;
        yRotation -= mouseY * sensY * multiplier;

        yRotation = Mathf.Clamp(yRotation, -90f, 90f);
    }

    void Jump(){
        rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);
    }

    void FixedUpdate()
    {
        if (!base.IsOwner){return;}
        
        if (isGrounded){
            rb.AddForce(moveDir * hareketHizi * movementmultiplier, ForceMode.Acceleration);
        }else{
            rb.AddForce(moveDir * hareketHizi * airmultiplier, ForceMode.Acceleration);
        }
    }
}