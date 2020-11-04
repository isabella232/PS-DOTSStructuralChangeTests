using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

#if false

[UpdateBefore(typeof(TransformSystemGroup))]
public class WeaponEquipSystem_HandlerUsingCustomEntityManagerV3 : SystemBase
{
    bool initialized = false;
    public bool IsInitialized()
    {
        return initialized;
    }

    public JobHandle commandQueueDependency;

    NativeQueue<CustomEntityManagerCommand>.ParallelWriter commandQueueFrontEnd;
    NativeQueue<CustomEntityManagerCommand> commandQueue;

    NativeList<Entity> weaponPrefabs;

    ComponentType weaponRefType;
    ComponentType parentType;
    ComponentType localToParentType;

    protected override void OnCreate()
    {
        EntityManager.CustomExtensionInit();

        commandQueue = new NativeQueue<CustomEntityManagerCommand>(Allocator.Persistent);
        commandQueueFrontEnd = commandQueue.AsParallelWriter();

        weaponPrefabs = new NativeList<Entity>(Allocator.Persistent);

        weaponRefType = new ComponentType(typeof(Weapon));
        parentType = new ComponentType(typeof(Parent));
        localToParentType = new ComponentType(typeof(LocalToParent));
    }

    protected override void OnDestroy()
    {
        weaponPrefabs.Dispose();

        commandQueue.Dispose();
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

        commandQueueDependency.Complete();
        EntityManager.DoCustomCommands_Batched(commandQueue.ToArray(Allocator.Temp));
        commandQueue.Clear();
    }

    public struct CommandReceiver
    {
        NativeQueue<CustomEntityManagerCommand>.ParallelWriter commandQueueFrontEnd;

        NativeList<Entity> weaponPrefabs;

        ComponentType parentType;
        ComponentType localToParentType;
        ComponentType weaponRefType;

        public CommandReceiver(WeaponEquipSystem_HandlerUsingCustomEntityManagerV3 parent)
        {
            commandQueueFrontEnd = parent.commandQueueFrontEnd;

            weaponPrefabs = parent.weaponPrefabs;

            parentType = parent.parentType;
            localToParentType = parent.localToParentType;
            weaponRefType = parent.weaponRefType;
        }

        public void AddCommand(Entity weaponOwner, Weapon weaponRef, int weaponIdx, in float4x4 weaponToOwner)
        {
            commandQueueFrontEnd.Enqueue(new CustomEntityManagerCommand
            {
                parent = weaponOwner,
                oldChildToDestroy = weaponRef.Value,
                prefabToInstantiateNewChildFrom = weaponPrefabs[weaponIdx],
                newChildToParentTransform = weaponToOwner,

                childToParentReferenceType = parentType,
                childToParentTransformType = localToParentType,
                parentToChildReferenceType = weaponRefType
            });
        }
    }
}

[UpdateBefore(typeof(WeaponEquipSystem_HandlerUsingCustomEntityManagerV3))]
public class WeaponEquipSystemTest_HandlerUsingCustomEntityManagerV3 : SystemBase
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

        public WeaponEquipSystem_HandlerUsingCustomEntityManagerV3.CommandReceiver commandReceiver;

        public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            var entityChunk = chunk.GetNativeArray(entityTypeHandle);
            var weaponRefChunk = chunk.GetNativeArray(weaponRefHandle);

            for (var i = 0; i < entityChunk.Length; i++)
            {
                var ownerEntity = entityChunk[i];
                var weaponRef = weaponRefChunk[i];

                commandReceiver.AddCommand(ownerEntity, weaponRef, weaponToEquipIdx, float4x4.identity);
            }
        }
    }

    protected override void OnUpdate()
    {
        var weaponEquipSystem = World.GetExistingSystem<WeaponEquipSystem_HandlerUsingCustomEntityManagerV3>();
        if (weaponEquipSystem.IsInitialized())
        {
            ++currentWeaponIdx;
            currentWeaponIdx = currentWeaponIdx % 4;

            var entityTypeHandle = GetEntityTypeHandle();
            var weaponRefHandle = GetComponentTypeHandle<Weapon>(true);

            var job = new WeaponEquipJob()
            {
                weaponToEquipIdx = currentWeaponIdx,

                entityTypeHandle = entityTypeHandle,
                weaponRefHandle = weaponRefHandle,

                commandReceiver = new WeaponEquipSystem_HandlerUsingCustomEntityManagerV3.CommandReceiver(weaponEquipSystem)
            };

            Dependency = job.ScheduleSingle(m_WeaponOwnerGroup, Dependency);
            weaponEquipSystem.commandQueueDependency = Dependency;
        }
    }
}

#endif
