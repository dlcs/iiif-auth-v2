import json
import os
import boto3

boto3.setup_default_session(profile_name='CHANGEME')
client = boto3.client('sns', 'eu-west-1')
topic = "arn:aws:sns:eu-west-1:000000000000:CHANGEME-customer-created"


def publish():
    item_json = os.path.join(os.path.dirname(__file__), 'sample.json')
    counter = 1

    with open(item_json) as json_file:
        data = json.load(json_file)
        items = len(data)

        for msg in data:

            print(f"Enqueuing message {msg['id']} - ({counter}/{items}):")
            response = client.publish(
                TopicArn=topic,
                Message=json.dumps(msg)
            )
            print(response)
            counter = counter + 1


if __name__ == "__main__":
    publish()
