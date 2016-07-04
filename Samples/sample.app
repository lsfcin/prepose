//////////////////////////////////////////////////////////////////////	
// sample gestures using prepose language
//////////////////////////////////////////////////////////////////////

APP sample:
	GESTURE trigger_test :
		POSE always_true :
			put your right wrist in front of your left hip.
		EXECUTION:
			always_true.
			
    GESTURE generic_left_punch : 
        POSE prepare_punch :
            put your left elbow behind your neck,
            put your left elbow to the left of your left shoulder.

        POSE execute_left_punch : 
            put your left elbow in front of your neck.

        EXECUTION : 
            prepare_punch,
            execute_left_punch.

    GESTURE wave : 
        POSE right_hand_to_right :
            put your right wrist to the right of your right elbow.

        POSE right_hand_to_left : 
            put your right wrist to the left of your right elbow.

        EXECUTION : 
            right_hand_to_right,
            right_hand_to_left,
            right_hand_to_right.