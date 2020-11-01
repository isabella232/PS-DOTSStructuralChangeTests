using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class WeaponDatabaseAuthoring : MonoBehaviour, IDeclareReferencedPrefabs
{
    public GameObject[] weaponPrefabs;

    public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
    {
        foreach (var entry in weaponPrefabs)
        {
            referencedPrefabs.Add(entry);
        }
    }
}

[InternalBufferCapacity(4)]
public struct WeaponDataEntry : IBufferElementData
{
    public Entity prefab;
}

[ConverterVersion("test", 1)]
[UpdateInGroup(typeof(GameObjectAfterConversionGroup))]
public class WeaponDatabaseConversion : GameObjectConversionSystem
{
    protected override void OnUpdate()
    {
        Entities.ForEach((WeaponDatabaseAuthoring weaponDatabaseAuth) =>
        {
            var entity = GetPrimaryEntity(weaponDatabaseAuth);

            var weaponDatabase = DstEntityManager.AddBuffer<WeaponDataEntry>(entity);
            foreach (var entry in weaponDatabaseAuth.weaponPrefabs)
            {
                var weaponPrefab = GetPrimaryEntity(entry);
                weaponDatabase.Add(new WeaponDataEntry { prefab = weaponPrefab });
            }
        });
    }
}
