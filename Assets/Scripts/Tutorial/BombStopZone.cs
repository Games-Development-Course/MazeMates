using UnityEngine;

private void OnTriggerEnter(Collider other)
{
    if (!_active) return;
    if (!other.CompareTag("Player")) return;

    var gm = GameManager.Instance;
    if (other.gameObject != gm.traveller) return;

    // נעל תנועה
    gm.travellerMove.SetFrozen(true);

    // סובב מצלמה
    LookAtBomb(gm.travellerCam);

    // *** הוספה חשובה ***
    FindObjectOfType<BombStepHelper>().OnTravellerReachedBombPoint();
}
