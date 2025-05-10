
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
public class Shoot : NetworkBehaviour
{

    public Transform gun;
    public float gunShake;
    public float gunSpeed;
    public float recoilUp;
    public float recoilBack;
    public Vector3 recoilVelocity, animVelocity;
    public float recoilLength;
    public float smooth;
    public float swayMultiplier;
    float time;
    Vector3 originalPos;
    Vector3 stayPosition;
    public GameObject rayPrefab;
    public Transform shootPoint;
    public Transform aimPoint;
    public float aimSwitchTime;
    public float tolerans = 0.02f;
    bool aiming = false;
    bool switchingAim = false;

    public Oyuncu player;

    bool recoiling, recovering;
    public SpriteRenderer spriteRenderer;
    public List<Sprite> flashes = new List<Sprite>();

    private readonly SyncVar<bool> shooting = new SyncVar<bool>(new SyncTypeSettings(WritePermission.ClientUnsynchronized, ReadPermission.ExcludeOwner));
    [ServerRpc(RunLocally = true)] private void SetShooting(bool value) => shooting.Value = value;

    public float knockbackTime;
    float ref1, ref2;

    void Start()
    {
        originalPos = gun.localPosition;
        stayPosition = originalPos;
    }

    public void GunVisibility(bool x)
    {
        gun.gameObject.SetActive(x);
    }

    void Update()
    {
        if(shooting.Value && time > gunSpeed)
        {
            time = 0;
            spriteRenderer.sprite = flashes[Random.Range(0, flashes.Count)];
        }
        else if(!shooting.Value)
        {
            spriteRenderer.sprite = null;
        }

        time += Time.deltaTime;

        if (!base.IsOwner) {return;}

        float mouseX = Input.GetAxisRaw("Mouse X") * -swayMultiplier;
        float mouseY = Input.GetAxisRaw("Mouse Y") * swayMultiplier;

        Quaternion rotationX = Quaternion.AngleAxis(mouseY, Vector3.right);
        Quaternion rotationY = Quaternion.AngleAxis(mouseX, Vector3.up);
        Quaternion targetRotation = rotationX * rotationY;
        gun.localRotation = Quaternion.Slerp(gun.localRotation, targetRotation, smooth * Time.deltaTime);

        if (Input.GetMouseButton(0))
        {
            if (time > gunSpeed)
            {
                //time = 0;
                // recoil and shoot
                recoiling = true;
                recovering = false;

                SetShooting(true);

                player.yRotation -= Random.Range(0.2f, 0.7f);
                player.xRotation += Random.Range(-0.7f, 0.7f);

                player.server_shoot(10);
            }
        }
        else
        {
            SetShooting(false);
        }

        if (Input.GetMouseButtonDown(1)) 
        {
            if (switchingAim){ return; }
            switchingAim = true;
            aiming = !aiming;
        }

        if (switchingAim){ switchAim(); }
        if (recoiling) { Recoil(); }
        if (recovering) { Recover(); }
    }

    void switchAim()
    {
        gun.localPosition = Vector3.SmoothDamp(gun.localPosition, aiming ? aimPoint.localPosition : stayPosition, ref animVelocity, aimSwitchTime);
        originalPos = aiming ? aimPoint.localPosition : stayPosition;
        if (Vector3.Distance(gun.localPosition, originalPos) < tolerans)
        {
            switchingAim = false;
            gun.localPosition = originalPos;
        }
    }

    void Recoil()
    {
        Vector3 finalPos = new Vector3(originalPos.x, originalPos.y + recoilUp, originalPos.z - recoilBack);

        gun.localPosition = Vector3.SmoothDamp(gun.localPosition, finalPos, ref recoilVelocity, recoilLength);

        if (gun.localPosition == finalPos)
        {
            recoiling = false;
            recovering = true;
        }
    }

    void Recover()
    {
        gun.localPosition = Vector3.SmoothDamp(gun.localPosition, originalPos, ref recoilVelocity, recoilLength);

        if (gun.localPosition == originalPos)
        {
            recoiling = false;
            recovering = false;
        }
    }
}
