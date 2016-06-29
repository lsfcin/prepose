//////////////////////////////////////////////////////////////////////	
// Presentation gestures
//////////////////////////////////////////////////////////////////////

APP present:
	GESTURE next_360:

		POSE main:
		put your right wrist to the left of your right shoulder,
		put your right wrist below your right elbow,
		put your right wrist in front of your right shoulder.
		
		POSE round_up:
		rotate your right wrist 15 degrees up.
		
		POSE round_front:
		rotate your right wrist 15 degrees to your front.
		
		POSE round_down:
		rotate your right wrist 15 degrees down.
		
		EXECUTION:
		main,
		round_up,
		main,
		round_front,
		main,
		round_down.

	GESTURE prev_360:

		POSE round_back:
		rotate your right wrist 15 degrees to your back.

		EXECUTION:
		main,
		round_up,
		main,
		round_back,
		main,
		round_down.