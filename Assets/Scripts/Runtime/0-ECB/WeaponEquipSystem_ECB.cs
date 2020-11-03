using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

#if false

[UpdateInGroup(typeof(InitializationSystemGroup))]
public class WeaponEquipSystem_ECB : SystemBase
{
    bool initialized = false;
    public bool IsInitialized()
    {
        return initialized;
    }

    NativeList<Entity> weaponPrefabs;

    protected override void OnCreate()
    {
        EntityManager.CustomExtensionInit();

        weaponPrefabs = new NativeList<Entity>(Allocator.Persistent);
    }

    protected override void OnDestroy()
    {
        weaponPrefabs.Dispose();
    }

    protected override void OnUpdate()
    {
        // Some initialization must happen on first update for singleton entity query to work
        if (!initialized)
        {
            initialized = true;

            var weaponDataEntity = GetSingletonEntity<WeaponDataEntry>();
            var weaponDatabase = EntityManager.GetBuffer<WeaponDataEntry>(weaponDataEntity);

            weaponPrefabs.Clear();
            for (int i = 0; i < weaponDatabase.Length; ++i)
            {
                weaponPrefabs.Add(weaponDatabase[i].prefab);
            }
        }
    }

    public struct CommandReceiver
    {
        EntityCommandBuffer commandBuffer;

        NativeList<Entity> weaponPrefabs;

        public CommandReceiver(WeaponEquipSystem_ECB parent, EntityCommandBuffer ecb)
        {
            commandBuffer = ecb;

            weaponPrefabs = parent.weaponPrefabs;
        }

        public void AddCommand(Entity weaponOwner, Weapon weaponRef, int weaponIdx, in float4x4 weaponToOwner)
        {
            if (weaponRef.Value != Entity.Null)
            {
                commandBuffer.DestroyEntity(weaponRef.Value);
            }

            var newWeapon = commandBuffer.Instantiate(weaponPrefabs[weaponIdx]);

            commandBuffer.AddComponent(newWeapon, new Parent { Value = weaponOwner });
            commandBuffer.AddComponent(newWeapon, new LocalToParent { Value = weaponToOwner });

            commandBuffer.SetComponent(weaponOwner, new Weapon { Value = newWeapon });
        }
    }
}

[UpdateInGroup(typeof(InitializationSystemGroup))]
public class WeaponEquipSystemTest_ECB : SystemBase
{
    int currentWeaponIdx;

    EntityQuery m_WeaponOwnerGroup;

    protected override void OnCreate()
    {
        m_WeaponOwnerGroup = GetEntityQuery(ComponentType.ReadOnly<Weapon>());
    }

    [BurstCompile]
    struct WeaponEquipJob : IJobChunk
    {
        public int weaponToEquipIdx;

        [ReadOnly]
        public EntityTypeHandle entityTypeHandle;

        [ReadOnly]
        public ComponentTypeHandle<Weapon> weaponRefHandle;

        public WeaponEquipSystem_ECB.CommandReceiver commandReceiver;

        public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            var entityChunk = chunk.GetNativeArray(entityTypeHandle);
            var weaponRefChunk = chunk.GetNativeArray(weaponRefHandle);

            for (var i = 0; i < entityChunk.Length; i++)
            {
                var weaponOwnerEntity = entityChunk[i];
                var refToWeapon = weaponRefChunk[i];

                commandReceiver.AddCommand(weaponOwnerEntity, refToWeapon, weaponToEquipIdx, float4x4.identity);
            }
        }
    }

    protected override void OnUpdate()
    {
        var weaponEquipSystem = World.GetExistingSystem<WeaponEquipSystem_ECB>();
        if (weaponEquipSystem.IsInitialized())
        {
            ++currentWeaponIdx;
            currentWeaponIdx = currentWeaponIdx % 4;

            var entityTypeHandle = GetEntityTypeHandle();
            var weaponRefHandle = GetComponentTypeHandle<Weapon>(true);

            var beginSimulationEcbSystem = World.GetExistingSystem<BeginSimulationEntityCommandBufferSystem>();
            var ecb = beginSimulationEcbSystem.CreateCommandBuffer();

            var job = new WeaponEquipJob()
            {
                weaponToEquipIdx = currentWeaponIdx,

                entityTypeHandle = entityTypeHandle,
                weaponRefHandle = weaponRefHandle,

                commandReceiver = new WeaponEquipSystem_ECB.CommandReceiver(weaponEquipSystem, ecb)
            };

            Dependency = job.ScheduleSingle(m_WeaponOwnerGroup, Dependency);
            beginSimulationEcbSystem.AddJobHandleForProducer(Dependency);
        }
    }
}

#endif
