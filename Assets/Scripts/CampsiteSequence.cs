// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  CampsiteSequence.cs — director del CAPÍTULO CAMPAMENTO (arranca al llegar
//  manejando desde la YPF). owner: manejás hasta un punto, de ahí el auto se
//  estaciona SOLO, una cámara cenital (3ª persona desde arriba) muestra el
//  campamento, y bajan todos scripteados. Después: se arman las carpas/fogata,
//  se hace de noche, comen/hablan, duermen, y Rufus se levanta y ve la Luz Mala.
//  Se construye por etapas (como YpfStorySequence). Lo dispara YpfStorySequence
//  al terminar de subir al auto (Etapa 7).
// ============================================================
using System.Collections;
using UnityEngine;

namespace FolkloreArchives
{
    public class CampsiteSequence : MonoBehaviour
    {
        OpeningDriveSequence op;
        CarController car;
        CarAutoDrive autoDrive;

        [Header("Llegada (coords del owner)")]
        public Vector3 driveTriggerPos = new Vector3(242.22f, 24.71229f, 199.8321f); // manejás hasta acá -> el auto sigue SOLO
        public float   driveTriggerRadius = 10f;
        public Vector2 campParkXZ = new Vector2(234.1218f, 213.4899f); // dónde FRENA/estaciona el auto (XZ, owner)
        public float   campParkY   = 24.0379f;            // altura del punto de estacionado
        public float   campParkYaw = -40.427f;           // yaw final del auto al estacionar

        [Header("Cámara 3ª persona")]
        public Vector3 overheadCamPos  = new Vector3(222.8298f, 25.25194f, 216.8119f); // posición de la cámara
        public Vector3 overheadCamLook = new Vector3(234.1218f, 24.6f, 213.4899f);     // mira al auto/donde bajan

        Camera _overhead;

        public void Begin(OpeningDriveSequence seq)
        {
            op = seq;
            car = Object.FindFirstObjectByType<CarController>();
            autoDrive = car != null ? car.GetComponent<CarAutoDrive>() : null;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            if (car == null) { Debug.LogWarning("[Camp] no encontré el auto"); yield break; }

            // 1) manejás vos hasta el TRIGGER. Si TODAVÍA no está seteado (falta la coord del owner),
            // el capítulo NO arranca -> el checkpoint 2 queda en la YPF como antes, y manejás normal.
            if (driveTriggerPos == Vector3.zero)
            {
                Debug.LogWarning("[Camp] falta driveTriggerPos -> el capítulo campamento no arranca (seteá el trigger).");
                yield break;
            }
            Debug.Log($"<color=cyan>[Camp] manejá hasta el campamento (trigger en {driveTriggerPos}, r={driveTriggerRadius})</color>");
            yield return new WaitUntil(() => Flat2(car.transform.position, driveTriggerPos) <= driveTriggerRadius);

            // 2) el auto se ESTACIONA SOLO: autopilot hasta el punto del campamento.
            Debug.Log("<color=cyan>[Camp] el auto se estaciona solo...</color>");
            PartyController.CinematicLock = true;   // no manejás ni movés cámara durante la llegada
            if (autoDrive != null)
            {
                autoDrive.waypoints = new[] { campParkXZ };
                autoDrive.hasFinalYaw = true;
                autoDrive.finalYaw = campParkYaw;
                car.autoPilot = true;
                autoDrive.active = true;

                // 3) cámara CENITAL mientras estaciona
                MakeOverheadCam();

                // drive-in corto (para ver que "estaciona"), después ASIENTO EXACTO en el punto:
                // el autopilot se traba en los árboles y no clava, así que al terminar teletransporto
                // el auto al punto/yaw que dio el owner (siempre queda ahí).
                Vector3 parkW = new Vector3(campParkXZ.x, campParkY, campParkXZ.y);
                float t = 0f;
                while (!autoDrive.HasArrived && t < 6f)
                {
                    if (t > 1f && Flat2(car.transform.position, parkW) <= 4f) break;
                    t += Time.deltaTime;
                    yield return null;
                }
                car.autoPilot = false;
                autoDrive.active = false;
                car.externalThrottle = 0f; car.externalSteer = 0f;

                // asentar EXACTO en el punto (posición + yaw), frenado del todo.
                var rb = car.GetComponent<Rigidbody>();
                Quaternion parkRot = Quaternion.Euler(0f, campParkYaw, 0f);
                car.transform.position = parkW;
                car.transform.rotation = parkRot;
                if (rb != null)
                {
                    rb.position = parkW; rb.rotation = parkRot;
                    if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                }
            }
            else
            {
                MakeOverheadCam();
            }

            Debug.Log("<color=cyan>[Camp] llegaron al campamento. (próximo: bajan y arman)</color>");
            // (próximas etapas: bajar scripteado, armar carpas/fogata, noche, comer, dormir, Rufus)

            // POR AHORA (solo está armada la llegada): devolver el control al jugador para que no
            // quede trabado. Cuando arme "bajan/arman/noche" se mantiene la cinemática hasta ahí.
            RestoreControl();
        }

        // devuelve el control: re-activa la cámara de la persona, saca el bloqueo y borra la cenital.
        void RestoreControl()
        {
            var party = Object.FindFirstObjectByType<PartyController>();
            if (party != null && party.personCam != null) party.personCam.gameObject.SetActive(true);
            if (_overhead != null) Destroy(_overhead.gameObject);
            _overhead = null;
            PartyController.CinematicLock = false;
        }

        // cámara cenital (3ª persona desde arriba del campamento). Apaga las cámaras de persona/perro
        // y activa esta. Se apaga al volver al control normal.
        void MakeOverheadCam()
        {
            if (_overhead != null) return;
            var go = new GameObject("CampOverheadCam");
            go.transform.position = overheadCamPos != Vector3.zero ? overheadCamPos
                                  : new Vector3(overheadCamLook.x, overheadCamLook.y + 28f, overheadCamLook.z - 14f);
            go.transform.LookAt(overheadCamLook);
            _overhead = go.AddComponent<Camera>();
            _overhead.tag = "MainCamera";
            _overhead.farClipPlane = 500f;
            go.AddComponent<AudioListener>();
            // apagar las cámaras del jugador/perro
            var party = Object.FindFirstObjectByType<PartyController>();
            if (party != null)
            {
                if (party.personCam != null) party.personCam.gameObject.SetActive(false);
                if (party.dogCam != null)    party.dogCam.gameObject.SetActive(false);
            }
        }

        static float Flat2(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }
    }
}
