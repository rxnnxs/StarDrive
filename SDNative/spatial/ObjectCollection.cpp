#include "ObjectCollection.h"
#include <stdexcept>

namespace spatial
{
    ObjectCollection::ObjectCollection() = default;
    ObjectCollection::~ObjectCollection() = default;

    void ObjectCollection::clear()
    {
        PendingInsert.clear();
        Objects.clear();
        MaxObjects = 0;
        FreeIds.clear();
    }

    uint32_t ObjectCollection::totalMemory() const
    {
        uint32_t bytes = PendingInsert.size() * sizeof(SpatialObject);
        bytes += Objects.size() * sizeof(SpatialObject);
        bytes += FreeIds.size() * sizeof(int);
        return bytes;
    }

    const SpatialObject& ObjectCollection::get(int objectId) const
    {
        if (objectId < (int)Objects.size())
        {
            return Objects.data()[objectId];
        }
        char err[128];
        snprintf(err, 128, "ObjectCollection::get objectId(%d) out of range(%d)",
                           objectId, Objects.size());
        throw std::out_of_range{err};
    }

    int ObjectCollection::insert(const SpatialObject& object)
    {
        int objectId;
        if (!FreeIds.empty())
        {
            objectId = FreeIds.back();
            FreeIds.pop_back();
        }
        else
        {
            objectId = MaxObjects++;
        }

        SpatialObject& pending = PendingInsert.emplace_back(object);
        // it MUST be active if it was inserted, we need this to be set 1
        pending.active = 1;
        pending.update = 1;
        pending.objectId = objectId;
        return objectId;
    }

    void ObjectCollection::remove(int objectId)
    {
        if (objectId < (int)Objects.size())
        {
            SpatialObject& o = Objects[objectId];
            if (o.active)
            {
                o.active = 0;
                o.update = 1;
                FreeIds.push_back(objectId);
                --NumActive;
            }
        }
        else
        {
            for (size_t i = 0; i < PendingInsert.size(); ++i)
            {
                if (PendingInsert[i].objectId == objectId)
                {
                    PendingInsert[i] = PendingInsert.back();
                    PendingInsert.pop_back();
                    FreeIds.push_back(objectId);
                    break;
                }
            }
        }
    }

    void ObjectCollection::update(int objectId, int x, int y)
    {
        if (objectId < (int)Objects.size())
        {
            SpatialObject& o = Objects[objectId];
            if (o.active && (o.x != x || o.y != y))
            {
                o.x = x;
                o.y = y;
                o.update = 1;
            }
        }
    }

    void ObjectCollection::submitPending()
    {
        if (!PendingInsert.empty())
        {
            if (Objects.size() < MaxObjects)
                Objects.resize(MaxObjects);

            while (!PendingInsert.empty())
            {
                SpatialObject& pending = PendingInsert.back();
                Objects[pending.objectId] = pending;
                PendingInsert.pop_back();
                ++NumActive;
            }
            PendingInsert.clear();
        }
    }
}
