using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages a single flower with nectar
/// </summary>
public class Flower : MonoBehaviour
{
    [Tooltip("The color when the flower is full")]
    public Color fullFlower = new Color(1f, 0f, 0f);

    [Tooltip("The color when the flower is empty")]
    public Color emptyFlower = new Color(.5f, 0f, 1f);

    /// <summary>
    /// The trigger collider representing the nectar
    /// </summary>
    [HideInInspector]
    public Collider nectarCollider;

    //The solid collider representing the flower petals
    private Collider flowerCollider;

    //the flower material
    private Material flowerMaterial;

    /// <summary>
    /// A vector pointing normal to the flower
    /// </summary>
    public Vector3 flowerUpVector
    {
        get
        {
            return nectarCollider.transform.up;
        }
    }

    /// <summary>
    /// Center position of the nectar collider
    /// </summary>
    public Vector3 flowerCenterPos
    {
        get
        {
            return nectarCollider.transform.position;
        }
    }

    /// <summary>
    /// Amount of nectar remaining in the flower
    /// </summary>
    public float nectarAmount { get; private set; }

    public bool hasNectar
    {
        get
        {
            return nectarAmount > 0f;
        }
    }

    /// <summary>
    /// Attemps to feed nectar from the flower
    /// </summary>
    /// <param name="amount">Amount of nectar to remove</param>
    /// <returns>Actual amount successfully removed</returns>
    public float Feed(float amount)
    {
        float nectarTaken = Mathf.Clamp(amount, 0f, nectarAmount);

        nectarAmount -= amount;

        if (nectarAmount <= 0)
        {
            nectarAmount = 0;
            //disable colliders
            flowerCollider.gameObject.SetActive(false);
            nectarCollider.gameObject.SetActive(false);

            //change color
            flowerMaterial.SetColor("_BaseColor", emptyFlower);
        }
        return nectarTaken;
    }

    /// <summary>
    /// Resets the flower
    /// </summary>
    public void ResetFlower()
    {
        nectarAmount = 1f;

        //enable colliders
        flowerCollider.gameObject.SetActive(true);
        nectarCollider.gameObject.SetActive(true);

        flowerMaterial.SetColor("_BaseColor", fullFlower);
    }

    /// <summary>
    /// called when the flower wakes up
    /// </summary>
    private void Awake()
    {
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        flowerMaterial = meshRenderer.material;

        flowerCollider = transform.Find("FlowerCollider").GetComponent<Collider>();
        nectarCollider = transform.Find("FlowerNectarCollider").GetComponent<Collider>();
    }
}
