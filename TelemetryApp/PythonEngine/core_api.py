import json
import pandas as pd
import skidpad_strategy
import event_detector # <-- Dodaliśmy nasz nowy detektor!

def process_session(file_path: str, competition_type: str, config_json: str) -> str:
    config = json.loads(config_json)
    
    try:
        df = pd.read_csv(file_path, sep=';', low_memory=False) 
    except Exception as e:
        return json.dumps({"error": f"Nie udało się wczytać pliku: {str(e)}"})

    # --- AUTOMATYCZNE WYKRYWANIE ---
    if competition_type == "Auto":
        # Python sam zgaduje konkurencję z danych!
        detected_comp = event_detector.detect_competition(df)
        competition_type = detected_comp
    else:
        detected_comp = competition_type # Użytkownik wymusił konkretny typ

    results = {}

    # --- SKIEROWANIE DO ODPOWIEDNIEGO SKRYPTU ---
    if competition_type == "Skidpad":
        results = skidpad_strategy.process(df, config)
        
    elif competition_type == "Acceleration":
        results = {"status": "success", "competition": "Acceleration", "message": "Zaimplementuj moduł accel!"}
        
    elif competition_type == "Autocross":
        results = {"status": "success", "competition": "Autocross", "message": "Zaimplementuj moduł autoX!"}
        
    elif competition_type == "Endurance":
        results = {"status": "success", "competition": "Endurance", "message": "Zaimplementuj moduł endurance!"}
        
    else:
        results = {"error": f"Nieobsługiwana konkurencja: {competition_type}"}

    # Zwracamy również informację o tym, co zostało wykryte (żeby wyświetlić w C#)
    if "metrics" in results:
        results["detected_event"] = detected_comp

    return json.dumps(results)