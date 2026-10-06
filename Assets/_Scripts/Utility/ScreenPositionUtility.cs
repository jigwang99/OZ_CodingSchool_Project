using UnityEngine;

public static class ScreenPositionUtility
{
    /// <summary>
    /// 화면 좌표를 지정한 Z 평면의 월드 좌표로 변환.
    /// 카메라에서 나온 광선과 평면이 만나지 않으면 false를 반환.
    /// </summary>
    public static Vector3 ScreenToWorld(Camera camera,Vector2 screenPosition,float boardZ)
    {
        // ScreenToWorldPoint의 Z는 카메라로부터의 거리.
        Vector3 position = new Vector3(screenPosition.x, screenPosition.y, Mathf.Abs(camera.transform.position.z - boardZ));
        Vector3 worldPosition = camera.ScreenToWorldPoint(position);
        worldPosition.z = boardZ;

        return worldPosition;
    }
}
