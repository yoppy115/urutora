import datetime
import os
import sys


def emit_error(message):
    print("ERROR=" + str(message).replace("\r", " ").replace("\n", " "))
    return 1


def class_matches(actual, requested):
    actual = (actual or "").lower()
    requested = requested.lower()
    if actual == requested or requested in actual:
        return True
    if requested.endswith("_bp_base_c"):
        return requested[:-len("base_c")] in actual
    return False


def integer_property(value):
    try:
        return int(value or 0)
    except (TypeError, ValueError):
        return 0


def fjordur_area(x, y, z):
    """Return a coarse Fjordur realm/island name for an Unreal world position."""
    latitude = 50.0 + (float(y) / 7140.0)
    longitude = 50.0 + (float(x) / 7140.0)

    # The three separate realms occupy distinct world-space altitude ranges.
    if (-435000.0 <= z <= -160000.0 and
            17.47 <= latitude <= 68.93 and 5.11 <= longitude <= 56.57):
        return "ASGARD"
    if (-190000.0 <= z <= -115000.0 and
            -9.15 <= latitude <= 33.73 and 62.50 <= longitude <= 105.38):
        return "VANAHEIM"
    if (-160000.0 <= z <= -100000.0 and
            54.48 <= latitude <= 97.36 and 21.35 <= longitude <= 64.23):
        return "JOTUNHEIM"

    # Midgard's named islands.  The rectangles are the official wiki's coarse
    # region bounds, so small coast/cave overlaps are intentionally described
    # as the nearest named island rather than pretending to be exact polygons.
    if ((68.0 <= longitude <= 100.0 and 68.0 <= latitude <= 100.0) or
            (66.0 <= longitude < 68.0 and 73.0 <= latitude <= 90.0)):
        return "MIDGARD_BALHEIMR"
    if 1.0 <= longitude <= 18.0 and 36.0 <= latitude <= 53.0:
        return "MIDGARD_BOLBJORD"
    if 0.0 <= longitude <= 51.0 and 48.0 <= latitude <= 100.0:
        return "MIDGARD_VARDILAND"
    if ((7.0 <= longitude <= 99.0 and 1.0 <= latitude <= 70.0) or
            (1.0 <= longitude < 7.0 and 4.0 <= latitude <= 40.0)):
        return "MIDGARD_VANNALAND"
    return "MIDGARD_OCEAN_CAVE"


def main():
    if len(sys.argv) != 4:
        return emit_error("Usage: ARK Dino Search.exe <save> <class> <wild|tamed|all>")

    save_path, class_name, category = sys.argv[1:4]
    if category not in ("wild", "tamed", "all"):
        return emit_error("Unknown category")
    if not os.path.isfile(save_path):
        return emit_error("Save file not found: " + save_path)

    try:
        from arkparser import WorldSave
        from arkparser.export import _save_lookup, _status_for

        world = WorldSave.load(save_path, lazy_properties=True)
        lookup = _save_lookup(world)
        matches = [
            obj for obj in world.objects
            if class_matches(obj.class_name, class_name)
        ]

        selected = []
        for obj in matches:
            team = obj.get_property_value("TargetingTeam", 0)
            is_tamed = isinstance(team, (int, float)) and team >= 50000
            if category == "tamed" and not is_tamed:
                continue
            if category == "wild" and is_tamed:
                continue
            selected.append(obj)

        saved_at = datetime.datetime.fromtimestamp(
            os.path.getmtime(save_path), datetime.timezone.utc
        ).isoformat()
        print("OK=1")
        print("COUNT=" + str(len(selected)))
        print("SAVED_AT=" + saved_at)

        locations = []
        save_name = os.path.basename(save_path).lower()
        is_fjordur = save_name == "fjordur.ark" or save_name.startswith("fjordur_")
        for obj in selected:
            loc = obj.location
            if loc is None:
                continue
            status = _status_for(obj, lookup)
            base_level = status.get_property_value("BaseCharacterLevel", 1) if status else 1
            extra_level = status.get_property_value("ExtraCharacterLevel", 0) if status else 0
            try:
                level = int(base_level or 1) + int(extra_level or 0)
            except (TypeError, ValueError):
                level = 1
            id1 = integer_property(obj.get_property_value("DinoID1", 0))
            id2 = integer_property(obj.get_property_value("DinoID2", 0))
            team = obj.get_property_value("TargetingTeam", 0)
            is_tamed = isinstance(team, (int, float)) and team >= 50000
            locations.append((level, id1, id2, is_tamed, loc.x, loc.y, loc.z))

        locations.sort(key=lambda item: (-item[0], item[4], item[5], item[6]))
        for level, id1, id2, is_tamed, x, y, z in locations[:100]:
            metadata = "DINOID={}:{} TYPE={}".format(
                id1, id2, "TAMED" if is_tamed else "WILD"
            )
            if is_fjordur:
                latitude = 50.0 + (float(y) / 7140.0)
                longitude = 50.0 + (float(x) / 7140.0)
                print("LOCATION=Lv.{}  {} AREA={} LAT={:.2f} LON={:.2f} X={:.1f} Y={:.1f} Z={:.1f}".format(
                    level, metadata, fjordur_area(x, y, z), latitude, longitude, x, y, z
                ))
            else:
                print("LOCATION=Lv.{}  {} X={:.1f} Y={:.1f} Z={:.1f}".format(
                    level, metadata, x, y, z
                ))
        return 0
    except Exception as exc:
        return emit_error(exc)


if __name__ == "__main__":
    raise SystemExit(main())
