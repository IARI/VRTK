﻿//====================================================================================
//
// Purpose: Provide ability to interact with interactable objects in the game world
//
// This script must be attached to a Controller within the [CameraRig] Prefab
//
// The SteamVR_ControllerEvents script must also be attached to the Controller
//
// For an object to be grabbale it must contain the SteamVR_InteractableObject script
// and have the isGrabbable flag set to true.
//
// Press the default 'Trigger' button on the controller to grab the object
// Released the default 'Trigger' button on the controller to release the object
//
//====================================================================================

using UnityEngine;
using System.Collections;

public struct ControllerInteractEventArgs
{
    public uint controllerIndex;
    public GameObject target;
}

public delegate void ControllerInteractEventHandler(object sender, ControllerInteractEventArgs e);

public class SteamVR_ControllerInteract : MonoBehaviour {
    public Rigidbody controllerAttachPoint = null;
    public Color globalTouchHighlightColor = Color.clear;

    public event ControllerInteractEventHandler ControllerTouchInteractableObject;
    public event ControllerInteractEventHandler ControllerUntouchInteractableObject;
    public event ControllerInteractEventHandler ControllerGrabInteractableObject;
    public event ControllerInteractEventHandler ControllerUngrabInteractableObject;

    private FixedJoint controllerAttachJoint;
    private GameObject touchedObject = null;
    private GameObject grabbedObject = null;

    private SteamVR_TrackedObject trackedController;

    public virtual void OnControllerTouchInteractableObject(ControllerInteractEventArgs e)
    {
        if (ControllerTouchInteractableObject != null)
            ControllerTouchInteractableObject(this, e);
    }

    public virtual void OnControllerUntouchInteractableObject(ControllerInteractEventArgs e)
    {
        if (ControllerUntouchInteractableObject != null)
            ControllerUntouchInteractableObject(this, e);
    }

    public virtual void OnControllerGrabInteractableObject(ControllerInteractEventArgs e)
    {
        if (ControllerGrabInteractableObject != null)
            ControllerGrabInteractableObject(this, e);
    }

    public virtual void OnControllerUngrabInteractableObject(ControllerInteractEventArgs e)
    {
        if (ControllerUngrabInteractableObject != null)
            ControllerUngrabInteractableObject(this, e);
    }

    ControllerInteractEventArgs SetControllerInteractEvent(GameObject target)
    {
        ControllerInteractEventArgs e;
        e.controllerIndex = (uint)trackedController.index;
        e.target = target;
        return e;
    }

    void Awake()
    {
        trackedController = GetComponent<SteamVR_TrackedObject>();
    }

    void Start () {
        if (GetComponent<SteamVR_ControllerEvents>() == null)
        {
            Debug.LogError("SteamVR_ControllerInteract is required to be attached to a SteamVR Controller that has the SteamVR_ControllerEvents script attached to it");
            return;
        }

        //If no attach point has been specified then just use the tip of the controller
        if (controllerAttachPoint == null)
        {
            controllerAttachPoint = transform.GetChild(0).Find("tip").GetChild(0).GetComponent<Rigidbody>();
        }

        //Create trigger box collider for controller
        BoxCollider collider = this.gameObject.AddComponent<BoxCollider>();
        collider.size = new Vector3(0.1f, 0.08f, 0.2f);
        collider.center = new Vector3(0f, -0.035f, -0.055f);
        collider.isTrigger = true;

        GetComponent<SteamVR_ControllerEvents>().AliasInteractOn += new ControllerClickedEventHandler(DoInteractObject);
        GetComponent<SteamVR_ControllerEvents>().AliasInteractOff += new ControllerClickedEventHandler(DoStopInteractObject);
    }

    void SnapObjectToGrabToController(GameObject obj)
    {
        obj.transform.position = controllerAttachPoint.transform.position;
        controllerAttachJoint = obj.AddComponent<FixedJoint>();
        controllerAttachJoint.connectedBody = controllerAttachPoint;
    }

    Rigidbody ReleaseGrabbedObjectFromController()
    {
        var jointGameObject = controllerAttachJoint.gameObject;
        var rigidbody = jointGameObject.GetComponent<Rigidbody>();
        Object.DestroyImmediate(controllerAttachJoint);
        controllerAttachJoint = null;

        return rigidbody;
    }

    void ThrowReleasedObject(Rigidbody rb, uint controllerIndex)
    {
        var origin = trackedController.origin ? trackedController.origin : trackedController.transform.parent;
        var device = SteamVR_Controller.Input((int)controllerIndex);
        if (origin != null)
        {
            rb.velocity = origin.TransformVector(device.velocity);
            rb.angularVelocity = origin.TransformVector(device.angularVelocity);
        }
        else
        {
            rb.velocity = device.velocity;
            rb.angularVelocity = device.angularVelocity;
        }
        rb.maxAngularVelocity = rb.angularVelocity.magnitude;
    }

    void GrabInteractedObject()
    {
        if (controllerAttachJoint == null && grabbedObject == null)
        {            
            grabbedObject = touchedObject;

            OnControllerGrabInteractableObject(SetControllerInteractEvent(grabbedObject));

            grabbedObject.GetComponent<SteamVR_InteractableObject>().Grabbed(this.gameObject);

            SnapObjectToGrabToController(grabbedObject);
        }
    }

    void UngrabInteractedObject(uint controllerIndex)
    {
        if (grabbedObject != null && controllerAttachJoint != null)
        {
            OnControllerUngrabInteractableObject(SetControllerInteractEvent(grabbedObject));

            grabbedObject.GetComponent<SteamVR_InteractableObject>().Ungrabbed(this.gameObject);

            grabbedObject = null;
            Rigidbody releasedObjectRigidBody = ReleaseGrabbedObjectFromController();
            ThrowReleasedObject(releasedObjectRigidBody, controllerIndex);
        }
    }

    void DoInteractObject(object sender, ControllerClickedEventArgs e)
    {
        if (touchedObject != null)
        {
            GrabInteractedObject();
        }
    }

    void DoStopInteractObject(object sender, ControllerClickedEventArgs e)
    {
        UngrabInteractedObject(e.controllerIndex);
    }

    bool IsObjectGrabbable(GameObject obj)
    {
        //The object must have the SteamVR_InteractableObject script attached to it
        return (obj.GetComponent<SteamVR_InteractableObject>() && obj.GetComponent<SteamVR_InteractableObject>().isGrabbable);
    }

    void OnTriggerStay(Collider collider)
    {
        if (grabbedObject == null && IsObjectGrabbable(collider.gameObject))
        {            
            touchedObject = collider.gameObject;
            OnControllerTouchInteractableObject(SetControllerInteractEvent(touchedObject));
            touchedObject.GetComponent<SteamVR_InteractableObject>().ToggleHighlight(true, globalTouchHighlightColor);
        }
    }

    void OnTriggerExit(Collider collider)
    {
        if (collider.GetComponent<SteamVR_InteractableObject>())
        {
            OnControllerUntouchInteractableObject(SetControllerInteractEvent(collider.gameObject));
            collider.GetComponent<SteamVR_InteractableObject>().ToggleHighlight(false);
        }
        touchedObject = null;
    }
}
