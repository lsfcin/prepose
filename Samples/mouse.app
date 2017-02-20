//////////////////////////////////////////////////////////////////////	
// sample gestures using prepose language
//////////////////////////////////////////////////////////////////////

APP sample:
    GESTURE mouse_x:
    	POSE x_max:
    	point your right wrist to your right.
    	
    	EXECUTION:
    	x_max.
    	
    GESTURE mouse_y:
    	POSE y_max:
    	point your right wrist down.
    	
    	EXECUTION:
    	y_max.
    	
   	GESTURE activate:
    	POSE left_on_air:
    	put your left hand tip above your left hip.
    	
    	EXECUTION:
    	left_on_air.
    	
   	GESTURE click:
   		POSE left_down:
    	put your left hand tip below your left elbow.
   	
    	POSE left_up:
    	put your left hand tip above your left elbow.
    	
    	EXECUTION:
    	left_down,
    	left_up.