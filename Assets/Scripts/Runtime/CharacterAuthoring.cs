using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class CharacterAuthoring : MonoBehaviour
{
}

// Character's reference to weapon
public struct Weapon : IComponentData
{
    public Entity Value;
}

[ConverterVersion("test", 1)]
[UpdateInGroup(typeof(GameObjectAfterConversionGroup))]
public class CharacterConversion : GameObjectConversionSystem
{
    protected override void OnUpdate()
    {
        Entities.ForEach((CharacterAuthoring characterAuthoring) =>
        {
            var entity = GetPrimaryEntity(characterAuthoring);
            DstEntityManager.AddComponentData(entity, new Weapon { Value = Entity.Null });
        });
    }
}
