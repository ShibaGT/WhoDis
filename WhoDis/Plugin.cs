using BepInEx;
using GorillaLocomotion;
using System;
using UnityEngine;
using static UnityEngine.Rendering.RayTracingAccelerationStructure;

namespace WhoDis
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        // made with love by tai/shibagt <3

        public static GameObject mainPanel, mainTabs, playerTabs, playerSelectionCapsule;
        public static Transform leftController
        { get { return GTPlayer.Instance.LeftHand.controllerTransform; } }
        public static Transform rightController
        { get { return GTPlayer.Instance.RightHand.controllerTransform; } }

        public static LineRenderer lr;
        public static Color selectionColor = new Color(0f, 0f, 1f, 0.5f);

        void Update()
        {
            if (ControllerInputPoller.instance.leftControllerGripFloat > 0.5f)
                pointerCast();
            else
                destroyPointer();
        }

        #region pointer
        void pointerCast()
        {
            Ray ray = new Ray(leftController.position, leftController.forward);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100f))
            {
                if (lr == null)
                {
                    var lrob = new GameObject("gunLine");
                    lr = lrob.AddComponent<LineRenderer>();
                    lr.endWidth = 0.01f;
                    lr.startWidth = 0.01f;
                    lr.material.shader = Shader.Find("GUI/Text Shader");
                    lr.material.color = selectionColor;
                }
                lr.SetPosition(0, leftController.position);
                lr.SetPosition(1, hit.point);

                var hitObject = hit.collider.gameObject;
                var player = hitObject.GetComponentInParent<VRRig>();
                if (player != null)
                {
                    // highlight player
                    if (playerSelectionCapsule == null)
                    {
                        playerSelectionCapsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                        Destroy(playerSelectionCapsule.GetComponent<Collider>());
                        var capsuleMat = new Material(Shader.Find("GUI/Text Shader"));
                        capsuleMat.color = selectionColor;
                        playerSelectionCapsule.GetComponent<MeshRenderer>().material = capsuleMat;
                        playerSelectionCapsule.transform.localScale = new Vector3(0.3f, 0.5f, 0.3f);
                    }
                    else
                    {
                        playerSelectionCapsule.transform.position = player.headConstraint.position;
                        playerSelectionCapsule.transform.rotation = player.transform.rotation;
                    }

                    // select player
                    if (ControllerInputPoller.instance.leftIndexPressed)
                        selectPlayer(player);
                }
                else
                {
                    if (playerSelectionCapsule != null)
                    {
                        Destroy(playerSelectionCapsule);
                        playerSelectionCapsule = null;
                    }
                }
            }
        }
        void destroyPointer()
        {
            if (lr != null)
            {
                Destroy(lr.gameObject);
                lr = null;
            }
            if (playerSelectionCapsule != null)
            {
                Destroy(playerSelectionCapsule);
                playerSelectionCapsule = null;
            }
        }
        #endregion

        #region panel

        void selectPlayer(VRRig player)
        {
            showPanel(player);
        }

        void showPanel()
        {
            // create main panel
            if (mainPanel == null)
            {
                mainPanel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mainPanel.transform.localScale = new Vector3(0.75f, 1, 0.2f);
            }
            mainPanel.transform.position = GTPlayer.Instance.headCollider.transform.position + GTPlayer.Instance.headCollider.transform.forward * 1.1f;
            mainPanel.transform.rotation = Quaternion.LookRotation(mainPanel.transform.position - GTPlayer.Instance.headCollider.transform.position);

            makeButtons();
        }

        void showPanel(VRRig player)
        {
            if (mainPanel == null)
            {
                showPanel();
            }
        }

        void destroyPanel()
        {
            if (mainPanel != null)
            {
                Destroy(mainPanel);
                mainPanel = null;
            }
        }

        #endregion

        #region buttons

        void makeButtons()
        {
            // create btm tabs
            if (mainTabs == null)
            {
                mainTabs = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mainTabs.transform.parent = mainPanel.transform;
                mainTabs.transform.localPosition = new Vector3(0, -1.2f, 0f);
                mainTabs.transform.localRotation = Quaternion.identity;
            }
        }

        #endregion
    }
}
