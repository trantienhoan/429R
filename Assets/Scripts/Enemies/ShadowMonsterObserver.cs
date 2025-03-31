using UnityEngine;
using Enemies;

public class ShadowMonsterObserver : MonoBehaviour
{
    private ShadowMonsterSpawner spawner;
    private ShadowMonster monster;
    private bool hasUnregistered = false;
    
    public void Setup(ShadowMonsterSpawner spawnerRef, ShadowMonster monsterRef)
    {
        spawner = spawnerRef;
        monster = monsterRef;
    }
    
    private void Update()
    {
        // If we already unregistered or something is null, do nothing
        if (hasUnregistered || spawner == null || monster == null)
            return;
            
        // Check if the monster is dead using the existing IsDead method
        if (monster.IsDead() || !monster.isActiveAndEnabled)
        {
            UnregisterFromSpawner();
        }
    }
    
    private void OnDestroy()
    {
        UnregisterFromSpawner();
    }
    
    private void UnregisterFromSpawner()
    {
        if (!hasUnregistered && spawner != null && monster != null)
        {
            spawner.UnregisterMonster(monster);
            hasUnregistered = true;
        }
    }
}